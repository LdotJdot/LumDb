using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.KeyIndex;
using LumDbEngine.Element.Structure.Page.Repo;
using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Manager.Common
{
    internal class NodeManager
    {
        public static RepoNode? GetRepoNode(DbCache db, in NodeLink nodeLink)
        {
            if (db.IsValidPage(nodeLink.TargetPageID) && nodeLink.TargetNodeIndex < RepoPage.NODES_PER_PAGE)
            {
                var page = PageManager.GetPage<RepoPage>(db, nodeLink.TargetPageID);

                return page?.Nodes[nodeLink.TargetNodeIndex];
            }

            return null;
        }

        //public static IndexNode? GetIndexNode(DbCache db, in NodeLink nodeLink)
        //{
        //    return GetIndexNode(db,nodeLink.TargetPageID,nodeLink.TargetNodeIndex);
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IndexNode? GetIndexNode(DbCache db, uint PageID, byte nodeIndex)
        {
            if (db.IsValidPage(PageID) && nodeIndex < IndexPage.NODES_PER_PAGE)
            {
                var page = PageManager.GetPage<IndexPage>(db, PageID);

                return page?.Nodes[nodeIndex];
            }
            else
            {
                return null;
            }
        }

        //public static bool GetIndexNodeDirectly(DbCache db, in NodeLink nodeLink, ref IndexNode outNode)
        //{
        //    if (db.IsValidPage(nodeLink.TargetPageID) && nodeLink.TargetNodeIndex < IndexPage.NODES_PER_PAGE)
        //    {
        //        if (db.pages.ContainsKey(nodeLink.TargetPageID))
        //        {
        //            var page = PageManager.GetPage<IndexPage>(db, nodeLink.TargetPageID);
        //            outNode.CopyExcludePage(page.Nodes[nodeLink.TargetNodeIndex]);
        //            return true;
        //        }
        //        else
        //        {
        //            using var reader= db.iof.RentReader();
        //            outNode.ReadFromStreamDirectly(reader, nodeLink.TargetPageID, nodeLink.TargetNodeIndex);
        //            reader.Dispose();
        //            return true;
        //        }
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}

        public static DataVarNode? GetDataVarNode(DbCache db, in NodeLink nodeLink)
        {
            if (db.IsValidPage(nodeLink.TargetPageID))
            {
                var page = PageManager.GetPage<DataVarPage>(db, nodeLink.TargetPageID);

                if (nodeLink.TargetNodeIndex < page.TotalDataCount)
                {
                    var node = page.DataVarNodes[nodeLink.TargetNodeIndex];

                    if (node.IsAvailable)
                    {
                        return node;
                    }
                }
            }
            return null;
        }

        public static bool IsEqual(in NodeLink left, in NodeLink right)
        {
            return left.TargetPageID == right.TargetPageID && left.TargetNodeIndex == right.TargetNodeIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEqual(in NodeLink left, in RepoNode right)
        {
            return left.TargetPageID == right.HostPageId && left.TargetNodeIndex == right.NodeIndex;
        }

        public static void SetLink(DbCache db, ref NodeLink nodeLink, in NodeLink link)
        {
            nodeLink.TargetPageID = link.TargetPageID;
            nodeLink.TargetNodeIndex = link.TargetNodeIndex;
        }

        public static void SetLink(DbCache db, ref NodeLink nodeLink, in RepoNode repoNode)
        {
            nodeLink.TargetPageID = repoNode.HostPageId;
            nodeLink.TargetNodeIndex = repoNode.NodeIndex;
        }

        public static void SetLink(DbCache db, ref NodeLink nodeLink, DataNode dataNode)
        {
            nodeLink.TargetPageID = dataNode.HostPageId;
            nodeLink.TargetNodeIndex = dataNode.NodeIndex;
        }
    }

    internal static class IndexNodeExtension
    {
        internal static void Update(in this IndexNode node, DbCache db)
        {
            var pg = PageManager.GetPage<IndexPage>(db, node.HostPageId);
            pg.MarkDirty();
            pg.Nodes[node.NodeIndex] = node;
        }
    }

    internal static class RepoNodeExtension
    {
        internal static void Update(in this RepoNode node, DbCache db)
        {
            var pg = PageManager.GetPage<RepoPage>(db, node.HostPageId);
            pg.MarkDirty();
            pg.Nodes[node.NodeIndex] = node;
        }

        internal static void Update(in this RepoNode node, DbCache db, byte nodeIndex)
        {
            var pg = PageManager.GetPage<RepoPage>(db, node.HostPageId);
            pg.MarkDirty();
            pg.Nodes[nodeIndex] = node;
        }
    }
}