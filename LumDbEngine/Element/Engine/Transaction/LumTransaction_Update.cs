using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Value;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbResult Update(string tableName, string keyName, object keyValue, string columnName, object value)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Update(db, tableName, keyName, keyValue, columnName, value);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbResult Update<T>(string tableName, string keyName, object keyValue, T value) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Update(db, tableName, keyName, keyValue, value);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbResult Update(string tableName, uint id, string columnName, object value)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Update(db, tableName, id, columnName, value);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbResult Update<T>(string tableName, uint id, T value) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Update(db, tableName, id, value);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbResult Update<T>(string tableName, Func<T, bool> condition, T value) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Update(db, tableName, value, condition);
            }
            catch
            {
                Discard();
                throw;
            }
        }
    }
}