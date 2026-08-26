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
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            // Validation runs before page mutation; do not Reset cache on throw (would wipe in-mem txn state).
            return dbManager.Insert(db, tableName, ValueConvert.ToTableValues(values));
        }
    }
}
