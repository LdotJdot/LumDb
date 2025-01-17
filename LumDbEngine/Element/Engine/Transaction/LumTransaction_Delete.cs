using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbValue Delete(string tableName, uint id)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Delete(db, tableName, id);
            }
            catch (Exception ex)
            {
                Discard();
                throw;
            }
        }

        public IDbValue Delete(string tableName, string keyName, object keyValue)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartWrite(rwLock);
                return dbManager.Delete(db, tableName, keyName, keyValue);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbResult Drop(string tableName)
        {
            CheckTransactionState();
            using var lk = LockTransaction.StartWrite(rwLock);
            try
            {
                return dbManager.Drop(db, tableName);
            }
            catch
            {
                db = new DbCache(iof, cachePages, dynamicCache);
                throw;
            }
        }
    }
}