using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

// #if !NATIVE_AOT


namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {      


        public IDbValue<T> Find
                     <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
(string tableName, string keyName, object keyValue) where T : class, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find<T>(db, tableName, keyName, keyValue);
            }
            catch
            {
                throw;
            }
        }

        public IDbValue<T> Find
                                 <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
(string tableName, uint id) where T : class, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.FindById<T>(db, tableName, id);
            }
            catch
            {
                throw;
            }
        }


        public IDbValues<T> Find
           <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
           (string tableName, Func<T, bool> condition) where T : class, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find<T>(db, tableName, condition, false, 0, 0);
            }
            catch
            {
                throw;
            }
        }

       public IDbValues<T> Find
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
        (string tableName, bool isBackward, uint skip, uint limit, Func<T, bool> condition) where T : class, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find<T>(db, tableName, condition, isBackward, skip, limit);
            }
            catch
            {
                throw;
            }
        }


    }
}

// #endif