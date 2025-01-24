using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Manager;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Value;
using System.Collections.Generic;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {

        public IDbValues<T> Where<T>(string tableName, (string keyName, Func<object, bool> checkFunc)[] conditions, bool isBackward = false, uint skip = 0, uint limit = 0) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                return dbManager.Where<T>(db, tableName, conditions, isBackward, skip, limit);
            }
            catch
            {
                throw;
            }
        }

        public IDbValues Where(string tableName, (string keyName, Func<object, bool> checkFunc)[] conditions, bool isBackward = false, uint skip = 0, uint limit = 0)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                return dbManager.Where(db, tableName, conditions, isBackward, skip, limit);
            }
            catch
            {
                throw;
            }
        }
        public IDbValue Count(string tableName, (string keyName, Func<object, bool> checkFunc)[] conditions)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                return dbManager.Count(db, tableName, conditions);
            }
            catch
            {
                throw;
            }
        }
    }

}