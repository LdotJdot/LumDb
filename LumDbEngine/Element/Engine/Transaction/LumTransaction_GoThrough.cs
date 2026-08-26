using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public void GoThrough(string tableName, RowViewAction action)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            dbManager.GoThrough(db, tableName, action);
        }

        public void GoThrough(string tableName, RowViewIdAction action)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            dbManager.GoThrough(db, tableName, action);
        }
    }
}
