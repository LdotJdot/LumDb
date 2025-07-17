using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Manager;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Extension.DbEntity;
using System.Collections.Generic;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
   

        public void GoThrough_Entity<T>(string tableName, Func<T, bool> action) where T : IDbEntity, new()
        {
            CheckTransactionState();

            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                dbManager.GoThrough_Entity<T>(db, tableName, action);
            }
            catch
            {
                throw;
            }
        }
    }

}