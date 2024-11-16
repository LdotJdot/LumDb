using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.KeyIndex;
using LumDbEngine.Utils.HashUtils;
using System.Diagnostics;

namespace LumDbEngine.Element.Manager.Specific
{
    internal static class IndexRepoManager
    {
        /*
            2048
            4096
            Mem: 61872kb, (5000) insert done elapse ms: 44184
            2048
            4096
            8192
            Mem: 85984kb, (10000) insert done elapse ms: 24850
            2048
            4096
            8192
            Mem: 115160kb, (100000) insert done elapse ms: 15841
            Mem: 279160kb, (1000000) insert done elapse ms: 13423
       */

        public static IndexNode BinarySearchAndInsertIndexNode(DbCache db, TablePage tablePage, in LumHash id, IndexNode? node)
        {
            IndexNode? newNode = null;

            (uint pageId, byte nodeIndex, bool isLeft, bool initialized) info = (uint.MaxValue, 0, false, false);

            while (true)
            {
                if (node == null)
                {
                    var indexPage = IndexManager.RequestAvailableIndexPage(db, tablePage);
                    indexPage.MarkDirty();
                    indexPage.Nodes[indexPage.AvailableNodeIndex].Id = id.HashValue;
                    newNode = indexPage.Nodes[indexPage.AvailableNodeIndex];
                    indexPage.AvailableNodeIndex++; // keep the node once created always
                    break;
                }

                var nodeValue = node.Value;

                var result = id.Compare(nodeValue.Id);

                if (result > 0)
                {
                    // right
                    var res = NodeManager.GetIndexNode(db, nodeValue.Right.TargetPageID, nodeValue.Right.TargetNodeIndex);

                    if (res == null)
                    {
                        info = (nodeValue.HostPageId, nodeValue.NodeIndex, false, true);
                        node = null;
                    }
                    else
                    {
                        node = res; // no change
                    }
                }
                else if (result < 0)
                {
                    // left
                    var res = NodeManager.GetIndexNode(db, nodeValue.Left.TargetPageID, nodeValue.Left.TargetNodeIndex);
                    if (res == null)
                    {
                        info = (nodeValue.HostPageId, nodeValue.NodeIndex, true, true);
                        node = null;
                    }
                    else
                    {
                        node = res;   // no change
                    }
                }
                else
                {
                    newNode = NodeManager.GetIndexNode(db, nodeValue.HostPageId, nodeValue.NodeIndex);
                    break;
                }
            }

            if (info.initialized)
            {
                var lastNode = NodeManager.GetIndexNode(db, info.pageId, info.nodeIndex);
                Debug.Assert(lastNode != null);

                if (info.isLeft)
                {
                    var tmpNode = lastNode.Value;
                    SetNodeLink(db, ref tmpNode.Left, newNode.Value);
                    tmpNode.Update(db);
                }
                else
                {
                    var tmpNode = lastNode.Value;

                    SetNodeLink(db, ref tmpNode.Right, newNode.Value);
                    tmpNode.Update(db);
                }
            }

            return newNode.Value;
        }

        public static IndexNode? BinarySearchIndexNode(DbCache db, in LumHash id, IndexNode? node)
        {
            while (true)
            {
                if (node.HasValue == false) break;

                var nodeValue = node.Value;

                var result = id.Compare(node.Value.Id);

                if (result > 0)
                {
                    // right
                    var rightNode = NodeManager.GetIndexNode(db, nodeValue.Right.TargetPageID, nodeValue.Right.TargetNodeIndex);

                    node = rightNode;
                }
                else if (result < 0)
                {
                    // left
                    var leftNode = NodeManager.GetIndexNode(db, nodeValue.Left.TargetPageID, nodeValue.Left.TargetNodeIndex);
                    node = leftNode;
                }
                else
                {
                    break;
                }
            }
            return node;
        }

        public static void SetNodeLink(DbCache db, ref NodeLink nodeLink, IndexNode node)
        {
            //db.MarkDirtyAndCachePage(nodeLink.Page);
            nodeLink.TargetPageID = node.HostPageId;
            nodeLink.TargetNodeIndex = node.NodeIndex;
        }
    }
}