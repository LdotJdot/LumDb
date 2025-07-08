using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Value;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbValue<uint> Insert(string tableName, (string columnName, object value)[] values)
        {
            // todo
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Insert(db, tableName, values);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }

        public IDbValue<uint> Insert<T>(string tableName, T t) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Insert(db, tableName, t);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }
    }
}