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
    internal class LumTransactionAsNoTracking : LumTransaction, ITransactionAsNoTracking
    {
        public LumTransactionAsNoTracking(IOFactory? iof, long cachePages, bool dynamicCache, DbEngine dbEngine)
        {
            this.dbEngine = dbEngine;
            Id = Guid.NewGuid();

            if (this.dbEngine.RegisterTransaction(Id, this))
            {

                rwLockLockTransaction = LockTransaction.StartRead(dbEngine.ReadWriteLock);

                if (dbEngine.disposed)
                {
                    LumException.Throw(LumExceptionMessage.DbEngDisposedEarly);
                }

                db = new DbCache(iof, cachePages, dynamicCache);

#if DEBUG
                LumException.ThrowIfTrue(dbEngine.disposed, "");
#endif
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
                    }                 
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