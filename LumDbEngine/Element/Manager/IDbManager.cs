using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;

namespace LumDbEngine.Element.Manager
{
    internal interface IDbManager
    {
        public IDbValue<uint> Insert(DbCache db, string tableName, (string columnName, object value)[] values);

        public IDbValue<uint> Insert<T>(DbCache db, string tableName, T t) where T : IDbEntity, new();

        public IDbValues Find(DbCache db, string tableName, Func<IEnumerable<object[]>, IEnumerable<object[]>> condition, bool isBackward);

        public IDbValues<T> Find<T>(DbCache db, string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward) where T : IDbEntity, new();

        public IDbValue Find(DbCache db, string tableName, string keyName, object keyValue);

        public IDbValue Find(DbCache db, string tableName, uint id);

        public IDbValue Delete(DbCache db, string tableName, uint id);

        public IDbValue Delete(DbCache db, string tableName, string keyName, object keyValue);

        public IDbValue<T> Find<T>(DbCache db, string tableName, string keyName, object keyValue) where T : IDbEntity, new();

        public IDbValue<T> FindById<T>(DbCache db, string tableName, uint id) where T : IDbEntity, new();

        public IDbResult Create(DbCache db, string tableName, (string columnName, DbValueType type, bool isKey)[] tableHeader);

        public IDbResult Update<T>(DbCache db, string tableName, T value, Func<T, bool> condition) where T : IDbEntity, new();

        public IDbResult Update<T>(DbCache db, string tableName, uint id, T value) where T : IDbEntity, new();

        public IDbResult Update(DbCache db, string tableName, uint id, string columnName, object value);

        public IDbResult Update<T>(DbCache db, string tableName, string keyName, object keyValue, T value) where T : IDbEntity, new();

        public IDbResult Update(DbCache db, string tableName, string keyName, object keyValue, string columnName, object value);

        public IDbResult Drop(DbCache db, string tableName);

        public IDbValues<T> Where<T>(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[]? conditions, bool isBackward, uint skip, uint limit) where T : IDbEntity, new();

        public IDbValues Where(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[]? conditions, bool isBackward, uint skip, uint limit);

        public IDbValue Count(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[] conditions);

    }
}