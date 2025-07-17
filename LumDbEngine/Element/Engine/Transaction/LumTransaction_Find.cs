using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbValue Find(string tableName, uint id)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock,dbEngine.TimeoutMilliseconds);
                return dbManager.Find(db, tableName, id);
            }
            catch
            {
                throw;
            }
        }

        public IDbValue Find(string tableName, string keyName, object keyValue)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find(db, tableName, keyName, keyValue);
            }
            catch
            {
                throw;
            }
        }

        public IDbValues Find(string tableName, Func<IEnumerable<object[]>, IEnumerable<object[]>> condition, bool isBackward)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find(db, tableName, condition, isBackward);
            }
            catch
            {
                throw;
            }
        }


        public IDbValues Find(string tableName, params (string keyName, Func<object, bool> checkFunc)[]? conditions)
        {
            if(conditions == null)
            {
                return new DbValues();
            }

            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find(db, tableName, conditions, false, 0, 0);
            }
            catch
            {
                throw;
            }
        }



        public IDbValues Find(string tableName, bool isBackward, uint skip, uint limit, params (string keyName, Func<object, bool> checkFunc)[]? conditions)
        {
            if (conditions == null)
            {
                return new DbValues();
            }

            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Find(db, tableName, conditions, isBackward, skip, limit);
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