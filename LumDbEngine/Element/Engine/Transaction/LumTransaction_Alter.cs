using LumDbEngine.Element.Engine.Lock;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Transaction
{
    internal partial class LumTransaction
    {
        public IDbResult AddColumn(string tableName, string columnName, DbValueType type, bool isKey, DbCell? defaultValue = null, int? ordinal = null)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                TableAlterManager.AddColumn(db, tableName, columnName, type, isKey, defaultValue, ordinal);
                return new DbResult();
            }
            catch (ColumnAlterRejected ex)
            {
                throw LumException.Raise(ex.Message);
            }
            catch (Exception ex)
            {
                // A rewrite already touched pages. Abort the transaction so Dispose cannot store
                // a half-rewritten table and SaveChanges fails loudly instead of committing it.
                MarkFaulted(ex);
                throw;
            }
        }

        public IDbResult DropColumn(string tableName, string columnName)
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartWrite(rwLock, dbEngine.TimeoutMilliseconds);
            try
            {
                TableAlterManager.DropColumn(db, tableName, columnName);
                return new DbResult();
            }
            catch (ColumnAlterRejected ex)
            {
                throw LumException.Raise(ex.Message);
            }
            catch (Exception ex)
            {
                MarkFaulted(ex);
                throw;
            }
        }

        public DbSpaceInfo GetSpace()
        {
            CheckTransactionState();
            using var lk = LockTransaction.TryStartRead(rwLock, dbEngine.TimeoutMilliseconds);
            var space = InspectSpace();
            return new DbSpaceInfo(space.LastPage, db.IsValidPage(space.FreePageHead), space.FileBytesEstimate);
        }
    }
}
