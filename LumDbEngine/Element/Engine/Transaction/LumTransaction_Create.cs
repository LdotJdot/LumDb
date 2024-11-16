using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbResult Create(string tableName, (string columnName, DbValueType type, bool isKey)[] tableHeader)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Create(db, tableName, tableHeader);
            }
            catch
            {
                Discard();
                throw;
            }
        }
    }
}