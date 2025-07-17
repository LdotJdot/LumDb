using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {

        public IDbResult Update_Entity<T>(string tableName, string keyName, object keyValue, T value) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Update_Entity(db, tableName, keyName, keyValue, value);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }

    

        public IDbResult Update_Entity<T>(string tableName, uint id, T value) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Update_Entity(db, tableName, id, value);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }

        public IDbResult Update_Entity<T>(string tableName, Func<T, bool> condition, T value) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Update_Entity(db, tableName, value, condition);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }
    }
}