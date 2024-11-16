using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;

namespace LumDbEngine.Element.Engine.Transaction
{
    public interface ITransaction : IDisposable
    {
        public IDbResult Create(string tableName, (string columnName, DbValueType type, bool isKey)[] tableHeader);

        public IDbResult Insert(string tableName, (string columnName, object value)[] values);

        public IDbResult Insert<T>(string tableName, T t) where T : IDbEntity, new();

        public IDbValues Find(string tableName, Func<IEnumerable<object[]>, IEnumerable<object[]>> condition);

        public IDbValue Find(string tableName, string keyName, object keyValue);

        public IDbValue Find(string tableName, uint id);

        public IDbValues<T> Find<T>(string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition) where T : IDbEntity, new();

        public IDbValue<T> Find<T>(string tableName, string keyName, object keyValue) where T : IDbEntity, new();

        public IDbValue<T> Find<T>(string tableName, uint id) where T : IDbEntity, new();

        public IDbValue Delete(string tableName, uint id);

        public IDbValue Delete(string tableName, string keyName, object keyValue);

        public IDbResult Drop(string tableName);

        public void SaveChanges();

        public void Commit();

        public void Discard();

        public IDbResult Update(string tableName, string keyName, object keyValue, string columnName, object value);

        public IDbResult Update<T>(string tableName, string keyName, object keyValue, T value) where T : IDbEntity, new();

        public IDbResult Update(string tableName, uint id, string columnName, object value);

        public IDbResult Update<T>(string tableName, uint id, T value) where T : IDbEntity, new();

        public IDbResult Update<T>(string tableName, Func<T, bool> condition, T value) where T : IDbEntity, new();
    }
}