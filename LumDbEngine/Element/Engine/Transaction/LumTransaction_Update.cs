using LumDbEngine.Element.Engine.Cache;
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
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Update(db, tableName, keyName, keyValue, columnName, value);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }

        public IDbResult Update<T>(string tableName, string keyName, object keyValue, T value) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Update(db, tableName, keyName, keyValue, value);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }

        public IDbResult Update(string tableName, uint id, string columnName, object value)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Update(db, tableName, id, columnName, value);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }

        public IDbResult Update<T>(string tableName, uint id, T value) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Update(db, tableName, id, value);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }

        public IDbResult Update<T>(string tableName, Func<T, bool> condition, T value) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Update(db, tableName, value, condition);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }
    }
}