using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbResult Create(string tableName, (string columnName, DbValueType type, bool isKey)[] tableHeader)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Create(db, tableName, tableHeader);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }


        public IDbResult Drop(string tableName)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.Drop(db, tableName);
            }
            catch
            {
                db.Reset();
                throw;
            }
        }

        public IDbValues<(string tableName, (string columnName, string dataType, bool isKey)[] columns)> GetTableNames()
        {
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                return dbManager.GetTableNames(db);

            }
            catch
            {
                db.Reset();
                throw;
            }
        }
    }
}