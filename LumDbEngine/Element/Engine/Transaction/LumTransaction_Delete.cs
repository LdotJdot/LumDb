﻿using LumDbEngine.Element.Engine.Cache;
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
                using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
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
                using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Delete(db, tableName, keyName, keyValue);
            }
            catch
            {
                Discard();
                throw;
            }
        }

    }
}