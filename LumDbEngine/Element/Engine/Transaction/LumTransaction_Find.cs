using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Value;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbValue Find(string tableName, uint id)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                return dbManager.Find(db, tableName, id);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbValue Find(string tableName, string keyName, object keyValue)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                return dbManager.Find(db, tableName, keyName, keyValue);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbValues Find(string tableName, Func<IEnumerable<object[]>, IEnumerable<object[]>> condition, bool isBackward)
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                return dbManager.Find(db, tableName, condition, isBackward);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbValues<T> Find<T>(string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward) where T : IDbEntity, new()
        {
            CheckTransactionState();
            //try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                return dbManager.Find(db, tableName, condition, isBackward);
            }
            //catch
            //{
            //    Discard();
            //    throw;
            //}
        }

        public IDbValue<T> Find<T>(string tableName, string keyName, object keyValue) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                return dbManager.Find<T>(db, tableName, keyName, keyValue);
            }
            catch
            {
                Discard();
                throw;
            }
        }

        public IDbValue<T> Find<T>(string tableName, uint id) where T : IDbEntity, new()
        {
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.StartRead(rwLock);
                return dbManager.FindById<T>(db, tableName, id);
            }
            catch
            {
                Discard();
                throw;
            }
        }
    }
}