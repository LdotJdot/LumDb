using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbValue<uint> Insert(string tableName, (string columnName, DbCell value)[] values)
        {
            if (values == null || values.Length == 0) return new DbValue<uint>(0);
            CheckTransactionState();
            try
            {
                using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
                return dbManager.Insert(db, tableName, ValueConvert.ToTableValues(values));
            }
            catch
            {
                // Bug fix BUG-10: if Insert fails partway through (e.g. column N validation fails after
                // pages 1..N-1 have already been mutated), discard the dirty in-memory state so the
                // failed Insert cannot be persisted by a subsequent SaveChanges. This mirrors the
                // LumTransaction_Delete pattern.
                Discard();
                throw;
            }
        }
    }
}
