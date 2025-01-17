using LumDbEngine.Element.Engine.Cache;
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
            using var lk = LockTransaction.StartWrite(rwLock);
            try
            {
                return dbManager.Create(db, tableName, tableHeader);
            }
            catch
            {
                db = new DbCache(iof, cachePages, dynamicCache);
                throw;
            }
        }
    }
}