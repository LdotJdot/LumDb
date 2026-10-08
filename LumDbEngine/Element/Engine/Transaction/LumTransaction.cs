using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Diagnostics;
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

        internal DbCache TransactionDb => db;

        internal DbDiagnostics.SpaceMetrics InspectSpace() => DbDiagnostics.Inspect(db);

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
            if (dbEngine.ReadWriteLock.IsUpgradeableReadLockHeld || dbEngine.ReadWriteLock.IsWriteLockHeld)
                throw LumException.Raise(LumExceptionMessage.IllegaTransaction);
            if (this.dbEngine.RegisterTransaction(Id, this))
            {
                try
                {
                    rwLockLockTransaction = LockTransaction.TryStartUpgradeableRead(dbEngine.ReadWriteLock, dbEngine.TimeoutMilliseconds);

                    if (Volatile.Read(ref dbEngine.disposed) != 0)
                    {
                        LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                    }

                    db = new DbCache(iof, cachePages, dynamicCache);
                }
                catch
                {
                    // Bug fix BUG-06: if DbCache ctor throws after the engine-wide upgradeable-read lock
                    // has been acquired, release the lock here so we do not leak it on the engine.
                    rwLockLockTransaction?.Dispose();
                    rwLockLockTransaction = null;
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
            LumException.ThrowIfTrue(Volatile.Read(ref disposed) != 0, "the current transaction is disposed");
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

        internal void SaveToMemory(LumDbEngine.IO.MemoryDbBuffer buffer)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            rwLockLockTransaction.WriteAction(() => db.WriteToMemory(buffer));
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

        // 0 = alive, 1 = disposed. Use Volatile/Interlocked for memory visibility across threads.
        protected private int disposed;

        void IDisposable.Dispose()
        {
            if (Volatile.Read(ref disposed) != 0) return;
            Volatile.Write(ref disposed, 1);
            try
            {
                if (Volatile.Read(ref dbEngine.disposed) != 0)
                {
                    LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                }

                // Bug fix BUG-03: use try/finally so that rwLockLockTransaction.Dispose() always runs,
                // even when db?.Dispose(dbEngine) (which flushes dirty pages) throws. Previously the
                // engine-wide upgradeable-read lock would be leaked on the exception path.
                try
                {
                    rwLockLockTransaction.WriteAction(() => db?.Dispose(dbEngine));
                }
                finally
                {
                    rwLockLockTransaction.Dispose();
                }
            }
            finally
            {
                dbEngine.UnregisterTransaction(Id);
            }
        }

    }
}