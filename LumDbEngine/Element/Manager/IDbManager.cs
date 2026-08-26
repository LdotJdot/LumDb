using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Manager
{
    internal partial interface IDbManager
    {
        IDbValue<uint> Insert(DbCache db, string tableName, TableValue[] values);

        IDbValue Find(DbCache db, string tableName, string keyName, DbCell keyValue);

        IDbValue Find(DbCache db, string tableName, uint id);

        IDbValue Delete(DbCache db, string tableName, uint id);

        IDbValue Delete(DbCache db, string tableName, string keyName, DbCell keyValue);

        IDbResult Create(DbCache db, string tableName, (string columnName, DbValueType type, bool isKey)[] tableHeader);

        IDbResult Update(DbCache db, string tableName, uint id, string columnName, DbCell value);

        IDbResult Update(DbCache db, string tableName, string keyName, DbCell keyValue, string columnName, DbCell value);

        IDbResult Drop(DbCache db, string tableName);

        void GoThrough(DbCache db, string tableName, RowViewAction action);

        void GoThrough(DbCache db, string tableName, RowViewIdAction action);

        IDbValues<(string tableName, (string columnName, string dataType, bool isKey)[])> GetTableNames(DbCache db);
    }
}
