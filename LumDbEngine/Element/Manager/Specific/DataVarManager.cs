using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using Microsoft.IO;
using System.Diagnostics;

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
            var dataVarPage = RequestAvailableDataVarPage(db);

            if (dataVarPage.RestPageSize < DataVarNode.HEADER_SIZE + DataVarNode.REDUNDANCY_SIZE)
            {
                dataVarPage = CreateDataVarPage(db);
            }

            SaveValueToDataVarPage(db, dataVarPage, data, 0);

            return (dataVarPage.PageId, (byte)(dataVarPage.TotalDataCount - 1));
        }

        private static void SaveValueToDataVarPage(DbCache db, DataVarPage dataVarPage, Span<byte> dataSpan, int offset = 0)
        {
            var len = Math.Max(dataSpan.Length, DataVarNode.REDUNDANCY_SIZE);

            while (true)
            {
                int rest = dataVarPage.RestPageSize - DataVarNode.HEADER_SIZE - len + offset;  // predict the rest

                db.MarkDirtyAndCachePage(dataVarPage);

                var dataVarNode = ExpandDataVarNode(dataVarPage);

                if (rest >= 0)
                {
                    dataVarNode.DataLength = dataSpan.Length - offset;
                    dataVarNode.TotalDataRestLength = len - offset;
                    dataVarNode.SpaceLength = Math.Max(dataVarNode.DataLength, DataVarNode.REDUNDANCY_SIZE);  // already make sure the redundancy size is sufficient.
                    dataVarPage.RestPageSize -= DataVarNode.HEADER_SIZE + dataVarNode.SpaceLength;
                    dataVarNode.InitializeData();
                    dataSpan.Slice(offset, dataVarNode.DataLength).CopyTo(dataVarNode.Data);

                    return;
                }
                else
                {
                    // write the next page.

                    dataVarNode.DataLength = dataVarPage.RestPageSize - DataVarNode.HEADER_SIZE;
                    dataVarNode.TotalDataRestLength = dataVarNode.DataLength;
                    dataVarNode.SpaceLength = dataVarNode.DataLength;
                    dataVarNode.InitializeData();

                    dataSpan.Slice(offset, dataVarNode.DataLength).CopyTo(dataVarNode.Data);
                    offset += dataVarNode.DataLength;
                    var nextPage = CreateDataVarPage(db);
                    PageManager.LinkPage(dataVarPage, nextPage);
                    dataVarPage.RestPageSize = 0;
                    dataVarPage = nextPage;
                }

                // go to next loop
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

        public static byte[] GetDataVar(DbCache db, NodeLink nodeLink)
        {
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

        internal static void UpdateData(DbCache db, ref NodeLink nodeLink, Span<byte> data)
        {
            UpdateValueToNodeLink(db, ref nodeLink, data);
        }

        private static void UpdateValueToNodeLink(DbCache db, ref NodeLink nodeLink, Span<byte> data, int offset = 0)
        {
            var len = data.Length;

            var dataVarNode = NodeManager.GetDataVarNode(db, nodeLink);

            if (dataVarNode.TotalDataRestLength >= data.Length || dataVarNode.TotalDataRestLength >= data.Length) // case 1, enough space, use existed
            {
                UpdateValueToDataVarNode(db, dataVarNode, nodeLink.TargetNodeIndex, data);
            }
            else  // case 2, not enough space, create new and update the nodeLink
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
            var dataSpan = data;
            while (true)
            {
                db.MarkDirtyAndCachePage(dataVarNode.Page);

                if (dataVarNode.SpaceLength >= data.Length - offset)    // store in current node.
                {
                    dataSpan.CopyTo(dataVarNode.Data.Slice(0, dataSpan.Length));
                    dataVarNode.DataLength = data.Length - offset;
                    dataVarNode.TotalDataRestLength = dataVarNode.SpaceLength;

                    if (nodeIndex == dataVarNode.Page.DataVarNodes.Length - 1 && db.IsValidPage(dataVarNode.Page.NextPageId))
                    {
                        var nextPage = PageManager.GetPage<DataVarPage>(db, dataVarNode.Page.NextPageId);
                        DeleteDataVarNode(db, new NodeLink() { TargetPageID = nextPage.PageId, TargetNodeIndex = 0 });
                    }
                    break;
                }
                else
                {
                    dataVarNode.DataLength = dataVarNode.SpaceLength;
                    dataVarNode.TotalDataRestLength = data.Length - offset;

                    dataSpan.Slice(offset, dataVarNode.DataLength).CopyTo(dataVarNode.Data);
                    // Array.Copy(data, offset, dataVarNode.Data, 0, dataVarNode.DataLength);
                    offset += dataVarNode.DataLength;

                    Debug.Assert(nodeIndex == dataVarNode.Page.DataVarNodes.Length - 1 && db.IsValidPage(dataVarNode.Page.NextPageId));

                    var nextPage = PageManager.GetPage<DataVarPage>(db, dataVarNode.Page.NextPageId);
                    dataVarNode = nextPage.DataVarNodes[0];
                }
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