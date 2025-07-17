using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
      
        public IDbValue<uint> Insert_Entity<T>(string tableName, T t) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Insert_Entity(db, tableName, t);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }
    }
}