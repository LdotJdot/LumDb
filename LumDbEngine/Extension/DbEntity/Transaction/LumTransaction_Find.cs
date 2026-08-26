using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbValues<T> Find_Entity<T>(string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward = false)
            where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.Find_Entity(db, tableName, condition, isBackward);
        }

        public IDbValue<T> Find_Entity<T>(string tableName, string keyName, DbCell keyValue) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.Find_Entity<T>(db, tableName, keyName, keyValue);
        }

        public IDbValue<T> Find_Entity<T>(string tableName, uint id) where T : IDbEntity, new()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.FindById_Entity<T>(db, tableName, id);
        }
    }
}
