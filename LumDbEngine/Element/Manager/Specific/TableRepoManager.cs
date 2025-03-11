using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Repo;
using LumDbEngine.Utils.ByteUtils;
using System.Diagnostics;

namespace LumDbEngine.Element.Manager.Specific
{
    internal static class TableRepoManager
    {
        public static TablePage GetTablePage(DbCache db, string tableName)
        {
            var tablePageId = TableRepoManager.FindTablePageId(db, tableName);

            if (tablePageId == uint.MaxValue)
            {
                return null;
            }
            return PageManager.GetPage<TablePage>(db, tablePageId);
        }

        public static RepoNode RequestAvailableTableRepoNode(DbCache db)
        {
            var page = RequestAvailableTableRepoPage(db);

            if (page?.HasAvailableNode() == true)
            {
                var node = page.Nodes[page.AvailableNodeIndex];
                page.AvailableNodeIndex += 1;
                page.MarkDirty();
                return node;
            }
            throw LumException.Raise($"Internal data error in [RequestAvailableTableRepoNode].");
        }

        public static RepoPage RequestAvailableTableRepoPage(DbCache db)
        {
            Debug.Assert(db.IsValidPage(db.AvailableTableRepo)); // Db was not initialized.;

            var page = PageManager.GetPage<RepoPage>(db, db.AvailableTableRepo);

            Debug.Assert(page != null); // Internal page error, getAvailableTableRepoPage;

            if (page!.HasAvailableNode() == false)
            {
                var tableRepoPage = PageManager.RequestAvailablePage<RepoPage>(db);
                db.SetAvailableTableRepoId(tableRepoPage.PageId);
                PageManager.LinkPage(page, tableRepoPage);   //Link
                page = tableRepoPage;
            }

            return page;
        }

        public static bool CreateNode(DbCache db, in RepoNodeKey key, out RepoNode resultNode)
        {
            var repoNode = Find(db, key);

            if (repoNode == null)
            {
                var node = RequestAvailableTableRepoNode(db);
                node.SetKey(key);
                node.Update(db);
                resultNode = node;
                return true;
            }
            else
            {
                resultNode = RepoNode.EmptyNode;
                return false;
            }
        }

        public static unsafe uint FindTablePageId(DbCache db, string tableName)
        {
            Span<byte> bsb = stackalloc byte[RepoNode.KeyLength];
            var tableNameBytes = tableName.PaddingToBytes(bsb);
            var res = Find(db, new RepoNodeKey(tableNameBytes));
            if (res == null)
            {
                return uint.MaxValue;
            }
            else
            {
                return res.Value.TargetLink.TargetPageID;
            }
        }

        public static unsafe RepoNode? FindTableRepoNode(DbCache db, string tableName)
        {
            Span<byte> bsb = stackalloc byte[RepoNode.KeyLength];
            var tableNameBytes = tableName.PaddingToBytes(bsb);
            return Find(db, new RepoNodeKey(tableNameBytes));
        }

        /// <summary>
        /// 如果找到则返回目标node，没找到为null
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="repoNode"></param>
        /// <returns></returns>
        private static RepoNode? Find(DbCache db, in RepoNodeKey tableName)
        {
            var page = PageManager.GetRootTableRepoPage(db);

            return FindIterate(db, page, tableName);
        }

        private static RepoNode? FindIterate(DbCache db, RepoPage? repoPage, in RepoNodeKey key)
        {
            while (true)
            {
                if (repoPage != null)
                {
                    for (int i = 0; i < RepoPage.NODES_PER_PAGE; i++)
                    {
                        if (db.IsValidPage(repoPage.Nodes[i].TargetLink.TargetPageID) && repoPage.Nodes[i].IsKeyEqual(key))
                        {
                            return repoPage.Nodes[i];
                        }
                    }

                    if (db.IsValidPage(repoPage.NextPageId))
                    {
                        repoPage = PageManager.GetPage<RepoPage>(db, repoPage.NextPageId);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return null;
        }

        internal static IList<RepoNode> IterateNodes(DbCache db)
        {
            var repoPage = PageManager.GetRootTableRepoPage(db);

            var nodes = new List<RepoNode>();

            while (true)
            {
                if (repoPage != null)
                {
                    for (int i = 0; i < RepoPage.NODES_PER_PAGE; i++)
                    {
                        if (db.IsValidPage(repoPage.Nodes[i].TargetLink.TargetPageID))
                        {
                            nodes.Add(repoPage.Nodes[i]);
                        }
                    }

                    if (db.IsValidPage(repoPage.NextPageId))
                    {
                        repoPage = PageManager.GetPage<RepoPage>(db, repoPage.NextPageId);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return nodes;
        }

        internal static void Drop(DbCache db, ref RepoNode node)
        {
            node.TargetLink.TargetPageID = uint.MaxValue;
            node.Update(db);
        }
    }
}