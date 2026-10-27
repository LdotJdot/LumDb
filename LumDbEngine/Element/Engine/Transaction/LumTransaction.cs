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

        /// <summary>Per-transaction gate for statement level locking. Thread-agnostic, like the engine gate.</summary>
        protected private EngineGate rwLock { get; } = new EngineGate();

        public Guid Id { get; protected set; } = Guid.NewGuid();

        /// <summary>Thread that created the transaction. Used to reject two live transactions on one thread.</summary>
        internal int OwnerThreadId;

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
        internal  LumTransaction(IOFactory? iof, long cachePages, bool dynamicCache, DbEngine dbEngine, bool bindLiveImage = false)
        {
            this.dbEngine = dbEngine;
            Id = Guid.NewGuid();
            OwnerThreadId = Environment.CurrentManagedThreadId;
            dbEngine.EnsureNoTransactionOnCurrentThread();

            this.dbEngine.RegisterTransaction(Id, this);

            try
            {
                rwLockLockTransaction = LockTransaction.TryStartUpgradeableRead(dbEngine.Gate, dbEngine.TimeoutMilliseconds);

                if (Volatile.Read(ref dbEngine.disposed) != 0)
                {
                    LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                }

                var image = bindLiveImage ? dbEngine.CurrentImage : iof;
                db = new DbCache(image, cachePages, dynamicCache);
            }
            catch
            {
                // Bug fix BUG-06: if DbCache ctor throws after the engine-wide lease has been
                // acquired, release it here so we do not leak it on the engine.
                rwLockLockTransaction?.Dispose();
                rwLockLockTransaction = null;
                this.dbEngine.UnregisterTransaction(Id);        // 构造函数异常时，确保事务被注销
                throw;
            }
        }

        // 0 = active, 1 = aborted by a failed statement. A faulted transaction can never be committed.
        protected private int faulted;
        protected private string faultReason = "";

        /// <summary>True when a statement failed inside this transaction; it can no longer be committed.</summary>
        internal bool IsFaulted => Volatile.Read(ref faulted) != 0;

        private void CheckDisposed()
        {
            LumException.ThrowIfTrue(Volatile.Read(ref disposed) != 0, "the current transaction is disposed");
        }

        private void CheckTransactionState()
        {
            CheckDisposed();
            if (Volatile.Read(ref faulted) != 0)
            {
                throw LumException.Raise(LumExceptionMessage.TransactionAborted + " First failure: " + faultReason);
            }
        }

        /// <summary>
        /// A statement failed half way: drop the in-memory working set and mark the transaction as
        /// aborted so a later SaveChanges cannot silently commit an empty/partial image.
        /// </summary>
        internal void MarkFaulted(Exception ex)
        {
            Interlocked.CompareExchange(ref faulted, 1, 0);
            if (string.IsNullOrEmpty(faultReason)) faultReason = ex.Message;

            try
            {
                db?.Reset();
            }
            catch
            {
                // best effort: the transaction is already aborted
            }
        }

        public void SaveChanges()
        {
            CheckTransactionState();
            // A failed commit is not a failed statement: the dirty pages are kept so the caller can
            // retry SaveChanges() after the disk problem is gone (replay is idempotent and the log
            // is kept when the replay itself fails).
            rwLockLockTransaction.WriteAction(() => db.SaveCurrentPageCache(dbEngine));
        }

        internal void SaveAs(string path)
        {
            CheckTransactionState();
            rwLockLockTransaction.WriteAction(() => db.SaveCurrentPageCache(path));
        }

        internal void SaveToMemory(LumDbEngine.IO.MemoryDbBuffer buffer)
        {
            CheckTransactionState();
            rwLockLockTransaction.WriteAction(() => db.WriteToMemory(buffer));
        }


        public void Discard()
        {
            CheckDisposed();
            rwLockLockTransaction.WriteAction(db.Reset);
            // An explicit rollback puts the transaction back into a usable state.
            Volatile.Write(ref faulted, 0);
            faultReason = "";
        }

        // 0 = alive, 1 = disposed. Use Volatile/Interlocked for memory visibility across threads.
        protected private int disposed;

        void IDisposable.Dispose()
        {
            if (Volatile.Read(ref disposed) != 0) return;
            Volatile.Write(ref disposed, 1);
            try
            {
                bool engineGone = Volatile.Read(ref dbEngine.disposed) != 0;

                // A faulted transaction must never be flushed: MarkFaulted dropped its working set.
                // An unmodified transaction has nothing to commit, so it must not take the engine
                // write lease either (that wait would time out while readers are open).
                if (!engineGone && Volatile.Read(ref faulted) == 0)
                {
                    if (db?.HasPendingChanges == true)
                        rwLockLockTransaction.WriteAction(() => db?.Dispose(dbEngine));
                    else
                        db?.Dispose();
                }

                if (engineGone)
                {
                    LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                }
            }
            finally
            {
                // Bug fix BUG-03: always return the engine lease, even when the flush above throws
                // (disk error) or the engine disposed check throws. A leaked write lock used to
                // brick the whole engine.
                rwLockLockTransaction?.Dispose();
                dbEngine.UnregisterTransaction(Id);
            }
        }

    }
}