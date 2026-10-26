using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using Microsoft.IO;
using System.Diagnostics;
using System.Threading;

namespace LumDbEngine.Element.Manager.Specific
{
    internal class DataVarManager
    {
        private static DataVarPage CreateDataVarPage(DbCache db)
        {
            var rootPage = PageManager.RequestAvailablePage<DataVarPage>(db);
            Debug.Assert(!db.IsValidPage(rootPage.NextPageId));
            db.SetAvailableDataVarPage(rootPage.PageId);
            return rootPage;
        }

        private static DataVarPage RequestAvailableDataVarPage(DbCache db)
        {
            if (db.IsValidPage(db.AvailableDataVarPage))
            {
                var page = PageManager.GetPage<DataVarPage>(db, db.AvailableDataVarPage);
                return page;
            }
            else
            {
                return CreateDataVarPage(db);
            }
        }

        public static (uint pageId, byte nodeIndex) InsertDataVar(DbCache db, Span<byte> data)
        {
            // Safe hole reuse: only when the whole payload fits one dead node.
            // Multi-page chaining is page-level (NextPageId) and only valid from the last
            // node on a page — reusing a middle hole for a chain would corrupt GetDataVar.
            if (TryReuseSingleNodeHole(db, data, out var reused))
            {
                return reused;
            }

            var dataVarPage = RequestAvailableDataVarPage(db);

            if (dataVarPage.RestPageSize < DataVarNode.HEADER_SIZE + DataVarNode.REDUNDANCY_SIZE)
            {
                dataVarPage = CreateDataVarPage(db);
            }

            SaveValueToDataVarPage(db, dataVarPage, data, 0);

            return (dataVarPage.PageId, (byte)(dataVarPage.TotalDataCount - 1));
        }

        /// <summary>
        /// Best-fit reuse of a deleted (!IsAvailable) node on the current AvailableDataVarPage
        /// when SpaceLength can hold the entire payload (no chaining).
        /// </summary>
        private static bool TryReuseSingleNodeHole(DbCache db, Span<byte> data, out (uint pageId, byte nodeIndex) result)
        {
            result = default;
            if (!db.IsValidPage(db.AvailableDataVarPage))
            {
                return false;
            }

            var page = PageManager.GetPage<DataVarPage>(db, db.AvailableDataVarPage);
            int bestIndex = -1;
            int bestSpace = int.MaxValue;

            for (int i = 0; i < page.TotalDataCount; i++)
            {
                var node = page.DataVarNodes[i];
                if (node.IsAvailable || node.SpaceLength < data.Length)
                {
                    continue;
                }

                if (node.SpaceLength < bestSpace)
                {
                    bestSpace = node.SpaceLength;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            db.MarkDirtyAndCachePage(page);
            var reuse = page.DataVarNodes[bestIndex];
            data.CopyTo(reuse.Data.Slice(0, data.Length));
            reuse.DataLength = data.Length;
            reuse.TotalDataRestLength = reuse.SpaceLength;
            reuse.IsAvailable = true;
            page.CurrentDataCount++;

            result = (page.PageId, (byte)bestIndex);
            return true;
        }

        /// <summary>
        /// Bytes this value will occupy on a page, including the 13-byte node header.
        /// A short value is stored in a 29-byte slot, so the fit check must use that
        /// slot and not the raw payload length. Using the raw length lets the node
        /// run past the 4096-byte page; the next page's type byte is then read back
        /// into the value (0x06) or left as the first byte of a page (unknown page type).
        /// </summary>
        private static int StoredSlotSize(int dataLength)
            => DataVarNode.HEADER_SIZE + Math.Max(dataLength, DataVarNode.REDUNDANCY_SIZE);

        private static void SaveValueToDataVarPage(DbCache db, DataVarPage dataVarPage, Span<byte> dataSpan, int offset = 0)
        {
            while (true)
            {
                var remaining = dataSpan.Length - offset;
                LumException.ThrowIfNotTrue(remaining >= 0, "dataVar offset past end");

                var slot = StoredSlotSize(remaining);
                if (dataVarPage.RestPageSize < slot)
                {
                    var rawRoom = dataVarPage.RestPageSize - DataVarNode.HEADER_SIZE;
                    // A fragment has no redundancy pad: it already fills the page.
                    // A short tail cannot be split that way, because its slot is the pad.
                    if (rawRoom > 0 && remaining > rawRoom && remaining > DataVarNode.REDUNDANCY_SIZE)
                    {
                        db.MarkDirtyAndCachePage(dataVarPage);
                        var fragment = ExpandDataVarNode(dataVarPage);
                        fragment.DataLength = rawRoom;
                        fragment.TotalDataRestLength = remaining;
                        fragment.SpaceLength = rawRoom;
                        fragment.InitializeData();
                        dataSpan.Slice(offset, rawRoom).CopyTo(fragment.Data);
                        dataVarPage.RestPageSize = 0;
                        offset += rawRoom;

                        var nextPage = CreateDataVarPage(db);
                        PageManager.LinkPage(dataVarPage, nextPage);
                        dataVarPage = nextPage;
                        continue;
                    }

                    dataVarPage = CreateDataVarPage(db);
                    continue;
                }

                db.MarkDirtyAndCachePage(dataVarPage);
                var dataVarNode = ExpandDataVarNode(dataVarPage);
                dataVarNode.DataLength = remaining;
                dataVarNode.TotalDataRestLength = remaining;
                dataVarNode.SpaceLength = Math.Max(remaining, DataVarNode.REDUNDANCY_SIZE);
                dataVarPage.RestPageSize -= DataVarNode.HEADER_SIZE + dataVarNode.SpaceLength;
                LumException.ThrowIfNotTrue(dataVarPage.RestPageSize >= 0, "dataVar node exceeds page");
                dataVarNode.InitializeData();
                if (remaining > 0)
                    dataSpan.Slice(offset, remaining).CopyTo(dataVarNode.Data);
                return;
            }
        }

        private static DataVarNode ExpandDataVarNode(DataVarPage dataVarPage)
        {
            var nodes = dataVarPage.DataVarNodes;
            dataVarPage.CurrentDataCount++;
            dataVarPage.TotalDataCount++;
            dataVarPage.DataVarNodes = new DataVarNode[dataVarPage.TotalDataCount];
            Array.Copy(nodes, dataVarPage.DataVarNodes, nodes.Length);
            dataVarPage.DataVarNodes[^1] = new DataVarNode(dataVarPage);
            var newNode = dataVarPage.DataVarNodes[^1];
            newNode.IsAvailable = true;

            return newNode;
        }

        internal static readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();

        /// <summary>Test/bench instrumentation: increments on every Var payload materialization.</summary>
        internal static long GetDataVarCallCount;

        public static byte[] GetDataVar(DbCache db, NodeLink nodeLink)
        {
            Interlocked.Increment(ref GetDataVarCallCount);
            // copy nodeLink
            using var sharedMem = recyclableMemoryStreamManager.GetStream();

            while (true)
            {
                var dataVarNode = NodeManager.GetDataVarNode(db, nodeLink);
                LumException.ThrowIfNull(dataVarNode, "internal dataVarPage error");

                sharedMem.Write(dataVarNode!.Data.Slice(0, dataVarNode.DataLength));

                if (nodeLink.TargetNodeIndex == dataVarNode.Page.DataVarNodes.Length - 1)
                {
                    if (db.IsValidPage(dataVarNode.Page.NextPageId))
                    {
                        var nextPage = PageManager.GetPage<DataVarPage>(db, dataVarNode.Page.NextPageId);
                        //nodeLink.Page = nextPage;
                        nodeLink.TargetPageID = nextPage.PageId;
                        nodeLink.TargetNodeIndex = 0;
                        continue;
                    }
                }

                var bt = sharedMem.ToArray();
                sharedMem.Dispose();
                return bt;
            }
        }

        private static int GetChainCapacity(DbCache db, in NodeLink start)
        {
            var link = start;
            int capacity = 0;

            while (true)
            {
                var node = NodeManager.GetDataVarNode(db, link);
                if (node == null)
                    return capacity;

                capacity += node.SpaceLength;

                if (link.TargetNodeIndex == node.Page.DataVarNodes.Length - 1
                    && db.IsValidPage(node.Page.NextPageId))
                {
                    var nextPage = PageManager.GetPage<DataVarPage>(db, node.Page.NextPageId);
                    link.TargetPageID = nextPage.PageId;
                    link.TargetNodeIndex = 0;
                }
                else
                {
                    break;
                }
            }

            return capacity;
        }

        private static void UnlinkContinuationPage(DbCache db, DataVarPage page)
        {
            if (!db.IsValidPage(page.NextPageId))
                return;

            var nextPageId = page.NextPageId;
            page.NextPageId = uint.MaxValue;
            page.MarkDirty();

            if (db.IsValidPage(nextPageId))
            {
                var nextPage = PageManager.GetPage<DataVarPage>(db, nextPageId);
                if (nextPage.PrevPageId == page.PageId)
                {
                    nextPage.PrevPageId = uint.MaxValue;
                    nextPage.MarkDirty();
                }
            }
        }

        internal static void UpdateData(DbCache db, ref NodeLink nodeLink, Span<byte> data)
        {
            UpdateValueToNodeLink(db, ref nodeLink, data);
        }

        private static void UpdateValueToNodeLink(DbCache db, ref NodeLink nodeLink, Span<byte> data, int offset = 0)
        {
            var dataVarNode = NodeManager.GetDataVarNode(db, nodeLink);
            LumException.ThrowIfNull(dataVarNode, "dataVar node internal error");

            if (GetChainCapacity(db, nodeLink) >= data.Length)
            {
                UpdateValueToDataVarNode(db, dataVarNode!, nodeLink.TargetNodeIndex, data);
            }
            else
            {
                DeleteDataVarNode(db, nodeLink);
                var res = InsertDataVar(db, data);
                nodeLink.TargetPageID = res.pageId;
                nodeLink.TargetNodeIndex = res.nodeIndex;
            }
        }

        /// <summary>
        /// overwrite the data bytes to the node
        /// </summary>
        /// <param name="db"></param>
        /// <param name="dataVarNode"></param>
        /// <param name="nodeIndex"></param>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        private static void UpdateValueToDataVarNode(DbCache db, DataVarNode dataVarNode, int nodeIndex, Span<byte> data, int offset = 0)
        {
            while (true)
            {
                db.MarkDirtyAndCachePage(dataVarNode.Page);

                int remaining = data.Length - offset;

                if (dataVarNode.SpaceLength >= remaining)
                {
                    data.Slice(offset, remaining).CopyTo(dataVarNode.Data.Slice(0, remaining));
                    dataVarNode.DataLength = remaining;
                    dataVarNode.TotalDataRestLength = dataVarNode.SpaceLength;

                    // Value ends here: unlink page chain first, then reclaim exclusive
                    // continuation starting at next-page node0. Shared pages keep other
                    // rows' nodes (Delete only marks node0 unavailable unless page empties).
                    if (nodeIndex == dataVarNode.Page.DataVarNodes.Length - 1
                        && db.IsValidPage(dataVarNode.Page.NextPageId))
                    {
                        var nextPageId = dataVarNode.Page.NextPageId;
                        UnlinkContinuationPage(db, dataVarNode.Page);
                        DeleteDataVarNode(db, new NodeLink
                        {
                            TargetPageID = nextPageId,
                            TargetNodeIndex = 0,
                        });
                    }

                    break;
                }

                dataVarNode.DataLength = dataVarNode.SpaceLength;
                dataVarNode.TotalDataRestLength = data.Length - offset;

                data.Slice(offset, dataVarNode.DataLength).CopyTo(dataVarNode.Data);
                offset += dataVarNode.DataLength;

                LumException.ThrowIfNotTrue(
                    nodeIndex == dataVarNode.Page.DataVarNodes.Length - 1 && db.IsValidPage(dataVarNode.Page.NextPageId),
                    "dataVar update chain exhausted");

                var nextPage = PageManager.GetPage<DataVarPage>(db, dataVarNode.Page.NextPageId);
                dataVarNode = nextPage.DataVarNodes[0];
                nodeIndex = 0;
            }
        }

        internal static void DeleteDataVarNode(DbCache db, NodeLink nodeLink)
        {
            // copy the nodeLink
            while (true)
            {
                var dataVarNode = NodeManager.GetDataVarNode(db, nodeLink);

                LumException.ThrowIfNull(dataVarNode, "dataVar node internal error");

                dataVarNode!.Page.MarkDirty();
                if (dataVarNode.SpaceLength > 0)
                    dataVarNode.Data.Clear();
                dataVarNode.DataLength = 0;
                dataVarNode.TotalDataRestLength = 0;
                dataVarNode.IsAvailable = false;
                dataVarNode.Page.CurrentDataCount--;

                var nextPageId = dataVarNode.Page.NextPageId;

                if (dataVarNode.Page.CurrentDataCount == 0)
                {
                    if (db.AvailableDataVarPage == dataVarNode.Page.PageId)
                    {
                        db.SetAvailableDataVarPage(uint.MaxValue);
                    }
                    PageManager.RecyclePage(db, dataVarNode.Page);
                }

                if (nodeLink.TargetNodeIndex == dataVarNode.Page.DataVarNodes.Length - 1 && db.IsValidPage(nextPageId))
                {
                    var nextPage = PageManager.GetPage<DataVarPage>(db, nextPageId);
                    //nodeLink.Page = nextPage;
                    nodeLink.TargetPageID = nextPage.PageId;
                    nodeLink.TargetNodeIndex = 0;
                    continue;
                }

                break;
            }
        }
    }
}