using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {       

        public IDbValues<T> Find_Entity<T>(string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find_Entity(db, tableName, condition, isBackward);
            }
            catch
            {
                throw;
            }
        }

        public IDbValue<T> Find_Entity<T>(string tableName, string keyName, object keyValue) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find_Entity<T>(db, tableName, keyName, keyValue);
            }
            catch
            {
                throw;
            }
        }

        public IDbValue<T> Find_Entity<T>(string tableName, uint id) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.FindById_Entity<T>(db, tableName, id);
            }
            catch
            {
                throw;
            }
        }

        public IDbValues<T> Find_Entity<T>(string tableName, params (string keyName, Func<object, bool> checkFunc)[]? conditions) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find_Entity<T>(db, tableName, conditions, false, 0, 0);
            }
            catch
            {
                throw;
            }
        }


        public IDbValues<T> Find_Entity<T>(string tableName, bool isBackward, uint skip, uint limit, params (string keyName, Func<object, bool> checkFunc)[]? conditions) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find_Entity<T>(db, tableName, conditions, isBackward, skip, limit);
            }
            catch
            {
                throw;
            }
        }

    }
}