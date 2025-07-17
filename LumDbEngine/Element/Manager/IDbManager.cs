using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Manager
{
    internal partial interface IDbManager
    {
        public IDbValue<uint> Insert(DbCache db, string tableName, (string columnName, object value)[] values);


        public IDbValues Find(DbCache db, string tableName, Func<IEnumerable<object[]>, IEnumerable<object[]>> condition, bool isBackward);

        public IDbValue Find(DbCache db, string tableName, string keyName, object keyValue);

        public IDbValue Find(DbCache db, string tableName, uint id);

        public IDbValue Delete(DbCache db, string tableName, uint id);

        public IDbValue Delete(DbCache db, string tableName, string keyName, object keyValue);

        public IDbResult Create(DbCache db, string tableName, (string columnName, DbValueType type, bool isKey)[] tableHeader);

        public IDbResult Update(DbCache db, string tableName, uint id, string columnName, object value);

        public IDbResult Update(DbCache db, string tableName, string keyName, object keyValue, string columnName, object value);

        public IDbResult Drop(DbCache db, string tableName);

        public IDbValues Find(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[]? conditions, bool isBackward, uint skip, uint limit);

        public IDbValue Count(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[] conditions);

        public void GoThrough(DbCache db, string tableName, Func<object[], bool> action);

        public IDbValues<(string tableName, (string columnName, string dataType, bool isKey)[])> GetTableNames(DbCache db);


    }
}