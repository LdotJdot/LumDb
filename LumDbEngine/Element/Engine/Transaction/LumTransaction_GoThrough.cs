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
   

        public void GoThrough<T>(string tableName, Func<T, bool> action) where T : IDbEntity, new()
        {
            CheckTransactionState();

            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                dbManager.GoThrough<T>(db, tableName, action);
            }
            catch
            {
                throw;
            }
        }

        public void GoThrough(string tableName, Func<object[], bool> action)
        {
            CheckTransactionState();

            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                dbManager.GoThrough(db, tableName, action);
            }
            catch
            {
                throw;
            }
        }

    }

}