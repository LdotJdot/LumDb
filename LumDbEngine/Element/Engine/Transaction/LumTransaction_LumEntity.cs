using LumDbEngine;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbResult Create<T>(string tableName) where T : ILumEntity<T>, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.CreateEntity<T>(db, tableName);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }

        public IDbValue<uint> Insert<T>(string tableName, T value) where T : ILumEntity<T>, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.InsertEntity(db, tableName, value);
        }

        public IDbValue<T> FindByIdEntity<T>(string tableName, uint id) where T : ILumEntity<T>, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.FindEntityById<T>(db, tableName, id);
        }

        public IDbValue<T> FindEntity<T>(string tableName, string keyName, DbCell keyValue) where T : ILumEntity<T>, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.FindEntity<T>(db, tableName, keyName, keyValue);
        }

        public IDbResult UpdateEntity<T>(string tableName, uint id, T value) where T : ILumEntity<T>, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.UpdateEntity(db, tableName, id, value);
        }

        public IDbResult UpdateEntity<T>(string tableName, string keyName, DbCell keyValue, T value) where T : ILumEntity<T>, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.UpdateEntity(db, tableName, keyName, keyValue, value);
        }

        public IDbValues<T> Find<T>(string tableName, RowViewPredicate predicate, uint skip = 0, uint limit = uint.MaxValue)
            where T : ILumEntity<T>, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.FindEntityWhere<T>(db, tableName, predicate, skip, limit);
        }

        public IDbValue<uint> Count(string tableName, RowViewPredicate predicate)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.CountWhere(db, tableName, predicate);
        }

        public IDbResult Update(string tableName, string keyName, DbCell keyValue, string columnName, DbCell value)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.Update(db, tableName, keyName, keyValue, columnName, value);
        }

        public IDbResult Update(string tableName, uint id, string columnName, DbCell value)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.Update(db, tableName, id, columnName, value);
        }

        public IDbValue Delete(string tableName, string keyName, DbCell keyValue)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Delete(db, tableName, keyName, keyValue);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }
    }
}
