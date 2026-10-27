using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Checker;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.IO;
using System.Linq.Expressions;
using System.Text;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal class TransactionReadonly : LumTransaction, ITransactionReadonly
    {
        public TransactionReadonly(IOFactory? iof, long cachePages, bool dynamicCache, DbEngine dbEngine, bool bindLiveImage = false)
        {
            this.dbEngine = dbEngine;
            Id = Guid.NewGuid();
            OwnerThreadId = Environment.CurrentManagedThreadId;

            this.dbEngine.RegisterTransaction(Id, this);

            try
            {
                rwLockLockTransaction = LockTransaction.TryStartRead(dbEngine.Gate, dbEngine.TimeoutMilliseconds);

                if (Volatile.Read(ref dbEngine.disposed) != 0)
                {
                    LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                }

                var image = bindLiveImage ? dbEngine.CurrentImage : iof;
                db = new DbCache(image, cachePages, dynamicCache);
#if DEBUG
                LumException.ThrowIfTrue(Volatile.Read(ref dbEngine.disposed) != 0, "");
#endif
            }
            catch
            {
                // Bug fix BUG-06: release the engine-wide read lock if acquired before the throw.
                rwLockLockTransaction?.Dispose();
                rwLockLockTransaction = null;
                this.dbEngine.UnregisterTransaction(Id);        // 构造函数异常时，确保事务被注销
                throw;
            }
        }

        void IDisposable.Dispose()
        {
            if (Volatile.Read(ref disposed) != 0) return;
            Volatile.Write(ref disposed, 1);
            try
            {
                // Bug fix BUG-03: ensure rwLockLockTransaction.Dispose() always runs, even when the
                // engine-wide disposed check below throws. Without the inner try/finally the engine-
                // wide read lock would leak on the exception path.
                try
                {
                    if (Volatile.Read(ref dbEngine.disposed) != 0)
                    {
                        LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                    }
                }
                finally
                {
                    rwLockLockTransaction?.Dispose();
                }
            }
            finally
            {
                dbEngine.UnregisterTransaction(Id);
            }
        }
    }
}