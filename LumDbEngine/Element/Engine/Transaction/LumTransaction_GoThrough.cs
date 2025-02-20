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
   

        public void GoThrough<T>(string tableName, Action<T> action) where T : IDbEntity, new()
        {
            CheckTransactionState();

            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                dbManager.GoThrough<T>(db, tableName, action);
            }
            catch
            {
                throw;
            }
        }

        public void GoThrough(string tableName, Action<object[]> action)
        {
            CheckTransactionState();

            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                dbManager.GoThrough(db, tableName, action);
            }
            catch
            {
                throw;
            }
        }

    }

}