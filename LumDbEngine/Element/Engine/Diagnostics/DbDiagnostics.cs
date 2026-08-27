using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Repo;

namespace LumDbEngine.Element.Engine.Diagnostics
{
    /// <summary>Internal space / hole metrics for tests and ConsoleTest churn simulation.</summary>
    internal static class DbDiagnostics
    {
        internal sealed record SpaceMetrics(
            int CachePages,
            int DataPages,
            int DataVarPages,
            int RepoPages,
            int DataSlotsLive,
            int DataSlotsCapacity,
            int VarNodesLive,
            int VarNodesDead,
            long VarDeadBytes,
            uint LastPage,
            uint FreePageHead,
            long FileBytesEstimate);

        internal static SpaceMetrics Inspect(DbCache db)
        {
            int dataLive = 0, dataCap = 0, varLive = 0, varDead = 0;
            long varDeadBytes = 0;
            int dataPages = 0, varPages = 0, repoPages = 0;

            foreach (var page in db.pages.Values)
            {
                switch (page)
                {
                    case DataPage dp:
                        dataPages++;
                        dataLive += dp.CurrentDataCount;
                        dataCap += dp.MaxDataCount;
                        break;
                    case DataVarPage vp:
                        varPages++;
                        for (int i = 0; i < vp.TotalDataCount; i++)
                        {
                            var n = vp.DataVarNodes[i];
                            if (n.IsAvailable)
                            {
                                varLive++;
                            }
                            else
                            {
                                varDead++;
                                varDeadBytes += n.SpaceLength;
                            }
                        }
                        break;
                    case RepoPage:
                        repoPages++;
                        break;
                }
            }

            uint last = db.LastPage;
            uint free = db.FreePage;
            long fileEst = DbHeader.HEADER_SIZE + (long)(last == uint.MaxValue ? 0 : last + 1) * BasePage.PAGE_SIZE;

            return new SpaceMetrics(
                db.pages.Count,
                dataPages,
                varPages,
                repoPages,
                dataLive,
                dataCap,
                varLive,
                varDead,
                varDeadBytes,
                last,
                free,
                fileEst);
        }
    }
}
