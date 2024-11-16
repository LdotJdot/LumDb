using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.KeyIndex;
using LumDbEngine.Element.Structure.Page.Repo;
using LumDbEngine.Utils.HashUtils;

namespace LumDbEngine.Element.Manager.Specific
{
    internal class IndexManager
    {
        public static byte InitializeIndexPageRoot(IndexPage page)
        {
            var nodeIndex = page.AvailableNodeIndex;
            page.MarkDirty();
            page.Nodes[nodeIndex].Id = IndexNode.ROOT_ID;
            //page.Nodes[nodeIndex].Page = page;
            page.Nodes[nodeIndex].NodeIndex = page.AvailableNodeIndex;
            page.AvailableNodeIndex = (byte)(nodeIndex + 1);    // IndexNode.ROOT_ID + 1

            return nodeIndex;
        }

        public static IndexPage RequestAvailableIndexPage(DbCache db, TablePage tablePage)
        {
            uint aip = tablePage.PageHeader.AvailableIndexPage;

            if (db.IsValidPage(aip))
            {
                var page = PageManager.GetPage<IndexPage>(db, aip);

                if (page.IsFree())
                {
                    return page;
                }
            }

            var newPage = PageManager.RequestAvailablePage<IndexPage>(db);
            tablePage.SetAvailableIndexPage(newPage.PageId);
            return newPage;
        }

        public static RepoNode? SearchAndDeleteKey(DbCache db, TablePage tablePage, in IndexNode? rootNode, Span<byte> key)
        {
            if (rootNode == null) return null;

            var id = LumHash.Create(key);

            var node = IndexRepoManager.BinarySearchIndexNode(db, id, rootNode);

            if (node.HasValue && db.IsValidPage(node.Value.KeyLink.TargetPageID))
            {
                var keyPage = PageManager.GetPage<RepoPage>(db, node.Value.KeyLink.TargetPageID);

                var resultKey = KeyManager.SearchKey(db, tablePage, node, new RepoNodeKey(key));

                if (resultKey != null)
                {
                    db.MarkDirtyAndCachePage(db, resultKey.Value.HostPageId);

                    var prevNode = NodeManager.GetRepoNode(db, resultKey.Value.PrevKeyNodeLink);
                    var nextNode = NodeManager.GetRepoNode(db, resultKey.Value.NextKeyNodeLink);

                    if (NodeManager.IsEqual(node.Value.KeyLink, resultKey.Value))
                    {
                        var newNode = node.Value;
                        NodeManager.SetLink(db, ref newNode.KeyLink, resultKey.Value.NextKeyNodeLink);
                        newNode.Update(db);
                    }
                    else if (prevNode != null && nextNode != null)
                    {
                        var newPrevNode = prevNode.Value;
                        NodeManager.SetLink(db, ref newPrevNode.NextKeyNodeLink, nextNode.Value);
                        newPrevNode.Update(db);

                        var newNextNode = nextNode.Value;
                        NodeManager.SetLink(db, ref newNextNode.PrevKeyNodeLink, prevNode.Value);
                        newNextNode.Update(db);
                    }
                    else if (prevNode != null)
                    {
                        var newPrevNode = prevNode.Value;
                        newPrevNode.NextKeyNodeLink.Reset();
                        newPrevNode.Update(db);
                    }

                    // free resultKey
                    var newResultKey = resultKey.Value;
                    newResultKey.Reset(newResultKey.HostPageId);
                    var nodeIndex = newResultKey.NodeIndex;
                    var repoPage = PageManager.GetPage<RepoPage>(db, newResultKey.HostPageId);
                    repoPage.MarkDirty();

                    var originNodeIndex = newResultKey.NodeIndex;
                    newResultKey.NodeIndex = repoPage.AvailableNodeIndex;
                    repoPage.AvailableNodeIndex = nodeIndex;
                    newResultKey.Update(db, originNodeIndex);    // chain the index, need the origin index
                    return newResultKey;
                }
            }
            return null;
        }

        public static DataNode? GetDataByIndex(DbCache db, TablePage tablePage, in IndexNode rootNode, Span<byte> key)
        {
            var id = LumHash.Create(key);

            var node = IndexRepoManager.BinarySearchIndexNode(db, id, rootNode);

            if (node != null)
            {
                var resultKey = KeyManager.SearchKey(db, tablePage, node, new RepoNodeKey(key));

                if (resultKey != null)
                {
                    var dataPage = PageManager.GetPage<DataPage>(db, resultKey.Value.TargetLink.TargetPageID);
                    var dataNode = dataPage.DataNodes[resultKey.Value.TargetLink.TargetNodeIndex];
                    if (dataNode.IsAvailable)
                    {
                        return dataNode;
                    }
                }
            }
            return null;
        }

        #region insertIndex

        public static void InsertMainIndex(DbCache db, TablePage tablePage, DataNode dataNode)
        {
            Span<byte> key = stackalloc byte[4];

            dataNode.Id.SerializeObjectToBytes(key);

            var hash = LumHash.Create(key);

            var root = NodeManager.GetIndexNode(db, tablePage.PageHeader.RootIndexNode.TargetPageID, tablePage.PageHeader.RootIndexNode.TargetNodeIndex);

            var indexNode = IndexRepoManager.BinarySearchAndInsertIndexNode(db, tablePage, hash, root);

            var keyNode = KeyManager.InsertKey(db, tablePage, indexNode, new RepoNodeKey(key));
            NodeManager.SetLink(db, ref keyNode.TargetLink, dataNode);
            keyNode.Update(db);
        }

        public static void InsertSubIndices(DbCache db, TablePage tablePage, DataNode dataNode, TableValue[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                InsertSubIndex(db, tablePage, dataNode, values[i].columnName);
            }
        }

        public static void InsertSubIndex(DbCache db, TablePage tablePage, DataNode dataNode, string columnName)
        {
            var headIndex = tablePage.GetTableHeaderIndex(columnName);
            var header = tablePage.ColumnHeaders[headIndex];

            if (header.IsKey)
            {
                var span = dataNode.Data.Slice(DataManager.GetDataOffset(tablePage.ColumnHeaders, headIndex), header.ValueType.GetLength());

                var hash = LumHash.Create(span);

                var root = NodeManager.GetIndexNode(db, header.RootSubIndexNode.TargetPageID, header.RootSubIndexNode.TargetNodeIndex);
                var indexNode = IndexRepoManager.BinarySearchAndInsertIndexNode(db, tablePage, hash, root);

                var keyNode = KeyManager.InsertKey(db, tablePage, indexNode, new RepoNodeKey(span));

                NodeManager.SetLink(db, ref keyNode.TargetLink, dataNode);
                keyNode.Update(db);
            }
        }

        #endregion insertIndex

        #region createIndices

        internal static void CreateIndices(DbCache db, TablePage tablePage, in TableHeaderInfo[] tableHeaderInfo)
        {
            CreateMainIndex(db, tablePage);
            CreateSubIndices(db, tablePage, in tableHeaderInfo);
        }

        private static void CreateMainIndex(DbCache db, TablePage tablePage)
        {
            var indexPage = PageManager.RequestAvailablePage<IndexPage>(db);
            var rootNodeIndex = InitializeIndexPageRoot(indexPage);
            tablePage.SetAvailableIndexPage(indexPage.PageId);
            tablePage.SetRootMainIndexPage(indexPage, rootNodeIndex);
        }

        private static unsafe void CreateSubIndices(DbCache db, TablePage tablePage, in TableHeaderInfo[] tableHeaderInfo)
        {
            foreach (var header in tableHeaderInfo)
            {
                if (header.isKey)
                {
                    CreateSubIndex(db, tablePage, header.keyName, header.type);
                }
            }
        }

        private static unsafe void CreateSubIndex(DbCache db, TablePage tablePage, byte* keyName, DbValueType type)
        {
            tablePage.MarkDirty();

            int keyIndex = -1;
            var keySpan = new Span<byte>(keyName, ColumnHeader.NameLength);
            for (int i = 0; i < tablePage.ColumnHeaders.Length; i++)
            {
                if (keySpan.SequenceEqual(tablePage.ColumnHeaders[i].Name))
                {
                    keyIndex = i;
                    break;
                }
            }

            LumException.ThrowIfTrue(keyIndex < 0, "Key not found:");
            var columnHeader = tablePage.ColumnHeaders[keyIndex];

            var indexPage = IndexManager.RequestAvailableIndexPage(db, tablePage);
            var rootNodeIndex = IndexManager.InitializeIndexPageRoot(indexPage);

            //columnHeader.RootSubIndexNode.Page = indexPage;
            columnHeader.RootSubIndexNode.TargetPageID = indexPage.PageId;
            columnHeader.RootSubIndexNode.TargetNodeIndex = rootNodeIndex;
        }

        #endregion createIndices

        #region deleteIndex

        public static void DeleteMainIndex(DbCache db, TablePage tablePage, DataNode dataNode)
        {
            // main index
            Span<byte> data = stackalloc byte[4];

            dataNode.Id.SerializeObjectToBytes(data);
            var rootIndexNode = NodeManager.GetIndexNode(db, tablePage.PageHeader.RootIndexNode.TargetPageID, tablePage.PageHeader.RootIndexNode.TargetNodeIndex);

            var idNode = SearchAndDeleteKey(db, tablePage, rootIndexNode, data);

            if (idNode == null) return;

            if (tablePage.PageHeader.AvailableRepoPage != idNode.Value.HostPageId)
            {
                var nodePage = PageManager.GetPage<RepoPage>(db, idNode.Value.HostPageId);
                nodePage.MarkDirty();
                nodePage.NextPageId = tablePage.PageHeader.AvailableRepoPage;
                tablePage.SetAvailableRepoPageId(nodePage.PageId);
            }
        }

        public static void DeleteSubIndices(DbCache db, TablePage tablePage, DataNode dataNode)
        {
            // sub indices
            int dataOffset = 0;

            for (int i = 0; i < tablePage.ColumnHeaders.Length; i++)
            {
                var columnHeader = tablePage.ColumnHeaders[i];
                var dataLenth = columnHeader.ValueType.GetLength();
                DeleteSubIndex(db, tablePage, columnHeader, dataNode.Data.Slice(dataOffset, dataLenth).ToArray());
                dataOffset += dataLenth;
            }
        }

        public static void DeleteSubIndex(DbCache db, TablePage tablePage, ColumnHeader columnHeader, byte[] keyAligned)
        {
            if (columnHeader.IsKey)
            {
                var rootIndexNode = NodeManager.GetIndexNode(db, columnHeader.RootSubIndexNode.TargetPageID, columnHeader.RootSubIndexNode.TargetNodeIndex);

                var subNode = SearchAndDeleteKey(db, tablePage, rootIndexNode, keyAligned);

                LumException.ThrowIfNotTrue(subNode.HasValue, "target not found");

                if (tablePage.PageHeader.AvailableRepoPage != subNode.Value.HostPageId)
                {
                    db.MarkDirtyAndCachePage(db, subNode.Value.HostPageId);
                    db.MarkDirtyAndCachePage(tablePage);
                    var nodePage = PageManager.GetPage<RepoPage>(db, subNode.Value.HostPageId);
                    nodePage.MarkDirty();
                    nodePage.NextPageId = tablePage.PageHeader.AvailableRepoPage;
                    tablePage.SetAvailableRepoPageId(subNode.Value.HostPageId);
                }
            }
        }

        #endregion deleteIndex

        internal static void UpdateIndex(DbCache db, TablePage tablePage, DataNode dataNode, ColumnHeader columnHeader, byte[] columnName, byte[] oldKey)
        {
            DeleteSubIndex(db, tablePage, columnHeader, oldKey);
            InsertSubIndex(db, tablePage, dataNode, (string)columnName.AsSpan().DeserializeBytesToValue(db, DbValueType.Str32B));
        }

        internal static void GetIndexPages(DbCache db, IndexNode? rootNode, HashSet<uint> pages)
        {
            Stack<IndexNode> stack = new Stack<IndexNode>(256);

            IndexNode? node = rootNode;
            while (node.HasValue)
            {
                var nodeValue = node.Value;

                pages.Add(nodeValue.HostPageId);

                KeyManager.GetRepoPages(db, nodeValue.KeyLink, pages);

                // right
                var rightNode = NodeManager.GetIndexNode(db, nodeValue.Right.TargetPageID, nodeValue.Right.TargetNodeIndex);
                if (rightNode.HasValue)
                {
                    stack.Push(rightNode.Value);
                }

                // left
                var leftNode = NodeManager.GetIndexNode(db, nodeValue.Left.TargetPageID, nodeValue.Left.TargetNodeIndex);
                if (leftNode.HasValue)
                {
                    stack.Push(leftNode.Value);
                }

                if (stack.TryPop(out var nextNode))
                {
                    node = nextNode;
                }
                else
                {
                    break;
                }
            }
        }
    }
}