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
        public IDbValues<T> Where<T>(string tableName, params (string keyName, Func<object, bool> checkFunc)[]? conditions) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Where<T>(db, tableName, conditions, false, 0, 0);
            }
            catch
            {
                throw;
            }
        }

        public IDbValues Where(string tableName, params (string keyName, Func<object, bool> checkFunc)[]? conditions)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Where(db, tableName, conditions, false, 0, 0);
            }
            catch
            {
                throw;
            }
        }


        public IDbValues<T> Where<T>(string tableName,  bool isBackward, uint skip, uint limit, params (string keyName, Func<object, bool> checkFunc)[]? conditions) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Where<T>(db, tableName, conditions, isBackward, skip, limit);
            }
            catch
            {
                throw;
            }
        }

        public IDbValues Where(string tableName, bool isBackward, uint skip, uint limit, params (string keyName, Func<object, bool> checkFunc)[]? conditions)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
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
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Count(db, tableName, conditions);
            }
            catch
            {
                throw;
            }
        }
    }

}