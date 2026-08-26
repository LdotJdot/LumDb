using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbValue Find(string tableName, uint id)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.Find(db, tableName, id);
        }

        public IDbValue Find(string tableName, string keyName, DbCell keyValue)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            return dbManager.Find(db, tableName, keyName, keyValue);
        }
    }
}
