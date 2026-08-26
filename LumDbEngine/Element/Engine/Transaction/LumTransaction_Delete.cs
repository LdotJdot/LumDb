using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

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
            catch
            {
                Discard();
                throw;
            }
        }
    }
}
