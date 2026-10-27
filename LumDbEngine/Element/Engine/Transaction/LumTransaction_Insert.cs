using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbValue<uint> Insert(string tableName, (string columnName, DbCell value)[] values)
        {
            if (values == null || values.Length == 0)
                return new DbValue<uint>(LumException.Raise(LumExceptionMessage.InsertValuesEmpty));
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Insert(db, tableName, ValueConvert.ToTableValues(values));
            }
            catch (Exception ex)
            {
                // A failed statement may have mutated pages 1..N-1. Mask the transaction as aborted:
                // the dirty state is dropped and a later SaveChanges throws instead of committing a
                // half-written image. The caller must Dispose (or Discard) the transaction.
                MarkFaulted(ex);
                throw;
            }
        }
    }
}
