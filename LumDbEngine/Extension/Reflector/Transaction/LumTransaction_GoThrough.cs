using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Manager;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Extension.DbEntity;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

// #if !NATIVE_AOT

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
   

        public void GoThrough
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, Func<T, bool> action) where T : class, new()
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
    }

}

// #endif
