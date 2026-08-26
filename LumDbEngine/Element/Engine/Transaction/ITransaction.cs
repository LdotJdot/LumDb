using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Engine.Transaction
{
    /// <summary>
    /// The transaction derived from db engine.
    /// Thread-safe via exclusive mutex for writers.
    /// </summary>
    public partial interface ITransaction : IDisposable
    {
        Guid Id { get; }

        IDbResult Create(string tableName, (string columnName, DbValueType type, bool isKey)[] tableHeader);

        IDbValue<uint> Insert(string tableName, (string columnName, DbCell value)[] values);

        IDbValue Find(string tableName, string keyName, DbCell keyValue);

        IDbValue Find(string tableName, uint id);

        IDbValue Delete(string tableName, uint id);

        IDbValue Delete(string tableName, string keyName, DbCell keyValue);

        IDbResult Drop(string tableName);

        void SaveChanges();

        void Discard();

        IDbResult Update(string tableName, string keyName, DbCell keyValue, string columnName, DbCell value);

        IDbResult Update(string tableName, uint id, string columnName, DbCell value);

        void GoThrough(string tableName, RowViewAction action);

        /// <summary>Scan rows and receive the engine row id (for Update/Delete by id).</summary>
        void GoThrough(string tableName, RowViewIdAction action);

        IDbValues<(string tableName, (string columnName, string dataType, bool isKey)[] columns)> GetTableNames();
    }
}
