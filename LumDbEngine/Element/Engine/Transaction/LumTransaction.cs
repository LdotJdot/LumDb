using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Checker;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.IO;
using System.Text;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction : ITransaction
    {
        private static IDbManager dbManager = new DbManager();
        private readonly DbCache db; // Unique for every single transaction.

        //private readonly STChecker checker;
        private readonly LockTransaction rwLockLockTransaction;
        private ReaderWriterLockSlim rwLock { get; } = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public Guid Id { get; } = Guid.NewGuid();

        internal int PagesCount => db.pages.Count;

        internal string DbState()
        {
            var sb = new StringBuilder();

            rwLockLockTransaction.WriteAction(() =>
            {
                sb.AppendLine("*******************************************");
                sb.AppendLine("total: " + db.pages.Values.Count());
                sb.AppendLine("table: " + db.pages.Values.Count(o => o.Type == PageType.Table));
                sb.AppendLine("data: " + db.pages.Values.Count(o => o.Type == PageType.Data));
                sb.AppendLine("index: " + db.pages.Values.Count(o => o.Type == PageType.Index));
                sb.AppendLine("repo: " + db.pages.Values.Count(o => o.Type == PageType.Respository));
                sb.AppendLine("dataVar: " + db.pages.Values.Count(o => o.Type == PageType.DataVar));
            });
            return sb.ToString();
        }

        private readonly IOFactory? iof;
        private readonly long cachePages;
        private readonly bool dynamicCache;
        private readonly DbEngine dbEngine;
        internal LumTransaction(IOFactory? iof,  long cachePages, bool dynamicCache, DbEngine dbEngine)
        {
            //this.checker=check;
            Id = Guid.NewGuid();
            this.dbEngine = dbEngine;
            rwLockLockTransaction = LockTransaction.StartUpgradeableRead(dbEngine.ReadWriteLock);

            db = new DbCache(iof, cachePages, dynamicCache);
            this.iof = iof;
            this.dynamicCache = dynamicCache;
            this.dbEngine.RegisterTransaction(Id, this);
        }

        private void CheckTransactionState()
        {
            LumException.ThrowIfTrue(disposed, "the current transaction is disposed");
        }

        public void SaveChanges()
        {
            CheckTransactionState();
            using var lk = LockTransaction.StartWrite(rwLock);
            rwLockLockTransaction.WriteAction(() => db.SaveCurrentPageCache(dbEngine));

        }

        internal void SaveAs(string path)
        {
            CheckTransactionState();
            using var lk = LockTransaction.StartWrite(rwLock);
            rwLockLockTransaction.WriteAction(() => db.SaveCurrentPageCache(path));
        }

        
        public void Discard()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                rwLockLockTransaction.WriteAction(db.Reset); 
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
                using var lk = LockTransaction.StartWrite(rwLock);

                disposed = true;
                try
                {
                    lock (dbEngine)
                    {
                        if (dbEngine.disposed)
                        {
                            LumException.Throw($"{LumExceptionMessage.DbEngDisposedEarly}:{Id.ToString()}");
                        }

                        rwLockLockTransaction.WriteAction(() => db?.Dispose(dbEngine));
                        rwLockLockTransaction.Dispose();
                        dbEngine.UnregisterTransaction(Id);
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
                
             }
        }
    }
}