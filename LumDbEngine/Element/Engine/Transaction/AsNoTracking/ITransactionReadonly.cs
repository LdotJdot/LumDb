using LumDbEngine;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Transaction.AsNoTracking
{
    /// <summary>
    /// Read-only transaction with concurrent readers.
    /// </summary>
    public partial interface ITransactionReadonly : IDisposable
    {
        Guid Id { get; }

        IDbValue Find(string tableName, string keyName, DbCell keyValue);

        IDbValue Find(string tableName, uint id);

        void GoThrough(string tableName, RowViewAction action);

        /// <summary>Scan rows and receive the engine row id.</summary>
        void GoThrough(string tableName, RowViewIdAction action);

        IDbValue<T> FindByIdEntity<T>(string tableName, uint id) where T : ILumEntity<T>, new();

        IDbValue<T> FindEntity<T>(string tableName, string keyName, DbCell keyValue) where T : ILumEntity<T>, new();

        IDbValues<T> Find<T>(string tableName, RowViewPredicate predicate, uint skip = 0, uint limit = uint.MaxValue)
            where T : ILumEntity<T>, new();

        IDbQueryable<T> Query<T>(string tableName) where T : ILumEntity<T>, new();

        IDbValues<T> Find<T>(string tableName, System.Linq.Expressions.Expression<Func<T, bool>> predicate, uint skip = 0, uint limit = uint.MaxValue, bool isBackward = false)
            where T : ILumEntity<T>, new();

        IDbValue<uint> Count(string tableName, RowViewPredicate predicate);

        IDbValue<uint> Count<T>(string tableName, System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : ILumEntity<T>, new();

        IDbValues<(string tableName, (string columnName, string dataType, bool isKey)[] columns)> GetTableNames();
    }
}
