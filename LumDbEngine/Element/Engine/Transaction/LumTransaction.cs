using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Checker;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.IO;
using System.Linq.Expressions;
using System.Text;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction : ITransaction
    {
        protected private static IDbManager dbManager = new DbManager();
        protected private DbCache db; // Unique for every single transaction.

        protected private LockTransaction rwLockLockTransaction;
        protected private ReaderWriterLockSlim rwLock { get; } = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public Guid Id { get; protected set; } = Guid.NewGuid();

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

        protected private DbEngine dbEngine;
        protected LumTransaction()
        {

        }
        internal  LumTransaction(IOFactory? iof, long cachePages, bool dynamicCache, DbEngine dbEngine)
        {
            this.dbEngine = dbEngine;
            Id = Guid.NewGuid();
            if (this.dbEngine.RegisterTransaction(Id, this))
            {
                try
                {
                    rwLockLockTransaction = LockTransaction.TryStartUpgradeableRead(dbEngine.ReadWriteLock, dbEngine.TimeoutMilliseconds);

                    if (dbEngine.disposed)
                    {
                        LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                    }

                    db = new DbCache(iof, cachePages, dynamicCache);
                }
                catch
                {
                    this.dbEngine.UnregisterTransaction(Id);        // 构造函数异常时，确保事务被注销
                    throw;
                }

            }
            else
            {
                throw LumException.Raise(LumExceptionMessage.DbEngDisposedEarly);
            }
        }

        private void CheckTransactionState()
        {
            LumException.ThrowIfTrue(disposed, "the current transaction is disposed");
        }

        public void SaveChanges()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            rwLockLockTransaction.WriteAction(() => db.SaveCurrentPageCache(dbEngine));

        }

        internal void SaveAs(string path)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            rwLockLockTransaction.WriteAction(() => db.SaveCurrentPageCache(path));
        }

        
        public void Discard()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
                rwLockLockTransaction.WriteAction(db.Reset); 
            }
            catch (Exception ex)
            {
                throw;

            }
        }

        protected private bool disposed = false;

        void IDisposable.Dispose()
        {
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);

            if (disposed == false)
            {
                disposed = true;
                try
                {
                    if (dbEngine.disposed)
                    {
                        LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                    }

                    rwLockLockTransaction.WriteAction(() => db?.Dispose(dbEngine));
                    rwLockLockTransaction.Dispose();
                }
                catch (Exception ex)
                {
                    throw;
                }
                finally
                {
                    dbEngine.UnregisterTransaction(Id);
                }

            }
        }

    }
}