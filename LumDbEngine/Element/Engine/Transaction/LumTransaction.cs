using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Checker;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.IO;
using System.Collections.Concurrent;
using System.Text;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction : ITransaction
    {
        private static IDbManager dbManager = new DbManager();
        private DbCache db; // Unique for every single transaction.
        private ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly STChecker checker;
        public Guid Id { get; } = Guid.NewGuid();
        internal int PagesCount => db.pages.Count;

        internal string DbState()
        {
            var sb = new StringBuilder();
            sb.AppendLine("*******************************************");
            sb.AppendLine("total: " + db.pages.Values.Count());
            sb.AppendLine("table: " + db.pages.Values.Count(o => o.Type == PageType.Table));
            sb.AppendLine("data: " + db.pages.Values.Count(o => o.Type == PageType.Data));
            sb.AppendLine("index: " + db.pages.Values.Count(o => o.Type == PageType.Index));
            sb.AppendLine("repo: " + db.pages.Values.Count(o => o.Type == PageType.Respository));
            sb.AppendLine("dataVar: " + db.pages.Values.Count(o => o.Type == PageType.DataVar));
            return sb.ToString();
        }

        private readonly IOFactory? iof;
        private readonly long cachePages;
        private readonly bool dynamicCache;
        private readonly DbEngine dbEngine;
        internal LumTransaction(IOFactory? iof, STChecker check, long cachePages, bool dynamicCache, DbEngine dbEngine)
        {
            this.checker=check;
            db = new DbCache(iof, cachePages, dynamicCache);
            this.iof = iof;
            this.dynamicCache = dynamicCache;
            Id = Guid.NewGuid();
            this.dbEngine = dbEngine;
            this.dbEngine.transactionsPool.TryAdd(Id, this);
        }

        private void CheckTransactionState()
        {
            LumException.ThrowIfTrue(disposed, "the current transaction is disposed");
        }

        public void SaveChanges()
        {
            CheckTransactionState();
            using (var lk = LockTransaction.StartWrite(rwLock))
            {
                db.SaveCurrentPageCache(dbEngine);
            }
        }

        internal void SaveAs(string path)
        {
            CheckTransactionState();
            using var lk = LockTransaction.StartWrite(rwLock);
            db.SaveCurrentPageCache(path);
        }

        
        public void Discard()
        {
            CheckTransactionState();
            try
            {

                using (var lk = LockTransaction.StartWrite(rwLock))
                {
                    db = new DbCache(iof, cachePages, dynamicCache);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private bool disposed = false;

        public void Dispose()
        {
            if (disposed == false)
            {
                disposed = true;
                try
                {
                    lock (dbEngine)
                    {
                        if (dbEngine.disposed)
                        {
                            LumException.Throw($"{LumExceptionMessage.DbEngDisposedEarly}:{Id.ToString()}");
                        }

                        using (var lk = LockTransaction.StartWrite(rwLock))
                        {
                            db?.Dispose();
                            db = null;
                        }
                        rwLock.Dispose();
                        dbEngine.transactionsPool.TryRemove(Id, out _);
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
                finally
                {
                    if (checker?.disposed == false)
                    {
                        checker?.Dispose();
                    }
                }
            }
        }
    }
}