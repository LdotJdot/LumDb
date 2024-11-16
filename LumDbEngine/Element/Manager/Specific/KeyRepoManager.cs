using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Repo;

namespace LumDbEngine.Element.Manager.Specific
{
    internal static class KeyRepoManager
    {
        public static RepoNode RequestAvailableRepoNode(DbCache db, TablePage tablePage)
        {
            var repoPage = RequestAvailableRepoPage(db, tablePage);

            if (repoPage?.HasAvailableNode() == true)
            {
                repoPage.MarkDirty();
                var nodeIndex = repoPage.AvailableNodeIndex;
                repoPage.AvailableNodeIndex = repoPage.Nodes[nodeIndex].NodeIndex == nodeIndex ? (byte)(nodeIndex + 1) : repoPage.Nodes[nodeIndex].NodeIndex; // use nodeIndex to link the chain
                repoPage.Nodes[nodeIndex].NodeIndex = nodeIndex;
                repoPage.Nodes[nodeIndex].Reset(repoPage.PageId);

                return repoPage.Nodes[nodeIndex];
            }

            throw LumException.Raise($"Internal data error in [RequestAvailableTableRepoNode].");
        }

        public static RepoPage RequestAvailableRepoPage(DbCache db, TablePage tablePage)
        {
            var arp = tablePage.PageHeader.AvailableRepoPage;
            var originArp = arp;
            RepoPage? page;

            while (true)
            {
                page = null;
                if (db.IsValidPage(arp))
                {
                    page = PageManager.GetPage<RepoPage>(db, arp);
                }

                if (page == null)  // if the page is full, request new one.
                {
                    var keyRepoPage = PageManager.RequestAvailablePage<RepoPage>(db);
                    tablePage.SetAvailableRepoPageId(keyRepoPage.PageId);
                    return keyRepoPage;
                }
                else if (page.HasAvailableNode() == false)
                {
                    arp = page.NextPageId;

                    if (arp == originArp) // to make sure not in a infinite loop
                    {
                        arp = uint.MaxValue;
                    }
                }
                else
                {
                    if (page.PageId != tablePage.PageHeader.AvailableRepoPage)
                    {
                        tablePage.SetAvailableRepoPageId(page.PageId);
                    }
                    return page;
                }
            }
        }
    }
}