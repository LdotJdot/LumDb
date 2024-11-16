using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Value;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbResult Insert(string tableName, (string columnName, object value)[] values)
        {
            // todo
            CheckTransactionState();

            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Insert(db, tableName, values);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbResult Insert<T>(string tableName, T t) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Insert(db, tableName, t);
            }
            catch
            {
                Discard();
                throw;
            }
        }
    }
}