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
        public TransactionReadonly(IOFactory? iof, long cachePages, bool dynamicCache, DbEngine dbEngine)
        {
            this.dbEngine = dbEngine;
            Id = Guid.NewGuid();

            if (this.dbEngine.RegisterTransaction(Id, this))
            {
                try
                {
                    rwLockLockTransaction = LockTransaction.TryStartRead(dbEngine.ReadWriteLock, dbEngine.TimeoutMilliseconds);

                    if (dbEngine.disposed)
                    {
                        LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                    }

                    db = new DbCache(iof, cachePages, dynamicCache);
#if DEBUG
                    LumException.ThrowIfTrue(dbEngine.disposed, "");
#endif
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

        void IDisposable.Dispose()
        {
            if (disposed == false)
            {
                disposed = true;
                try
                {
                    if (dbEngine.disposed)
                    {
                        LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                        db.Dispose();
                    }
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