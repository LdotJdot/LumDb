using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Value;

namespace LumDbEngine.Element.Engine.Transaction
{
    /// <summary>
    /// The transaction derived from db engine.<br/>
    /// Multiple transactions can be executed concurrently under the read-write lock model.
    /// </summary>
    public interface ITransaction : IDisposable
    {
        /// <summary>
        /// Create a new table.
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="tableHeader">A tuple array to describe each column in the table header.<br/>
        /// Each tuple should has a name of the column, a data type of the column, and whether as a key </param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Create(string tableName, (string columnName, DbValueType type, bool isKey)[] tableHeader);

        /// <summary>
        /// Insert values to a table
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="values">A tuple array to describe the line data. <br/>
        /// Each tuple should has the column name and the insert value </param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Insert(string tableName, (string columnName, object value)[] values);

        /// <summary>
        /// Insert values to a table
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="t">target instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Insert<T>(string tableName, T t) where T : IDbEntity, new();

        /// <summary>
        /// Search the results in table
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="condition">the match condition of the iterate search</param>
        /// <returns>DbValues, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues Find(string tableName, Func<IEnumerable<object[]>, IEnumerable<object[]>> condition);

        /// <summary>
        /// Search a result in table by key name and key value
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="keyName">target key name</param>
        /// <param name="keyValue">target key value</param>
        /// <returns>DbValue, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue Find(string tableName, string keyName, object keyValue);

        /// <summary>
        /// Search a result in table by key name and key value
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="id">target id. <br/>
        /// Each data in table has a unique ordered uint32 id, and could be used as a index</param>
        /// <returns>DbValue, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue Find(string tableName, uint id);

        /// <summary>
        /// Search the results in table
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="condition">the match condition of the iterate search.<br/>
        /// e.g. o=>o.Where(l=>foo(l.name))</param>
        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues<T> Find<T>(string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition) where T : IDbEntity, new();

        /// <summary>
        /// Search a result in table by key name and key value
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="keyName">target key name</param>
        /// <param name="keyValue">target key value</param>
        /// <returns>DbValue of T, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<T> Find<T>(string tableName, string keyName, object keyValue) where T : IDbEntity, new();

        /// <summary>
        /// Search a result in table by key name and key value
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="id">target id. <br/>
        /// Each data in table has a unique ordered uint32 id, and could be used as a index</param>
        /// <returns>DbValue of T, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<T> Find<T>(string tableName, uint id) where T : IDbEntity, new();

        /// <summary>
        /// Delete a data by default id.<br/>
        /// Each data in table has a unique ordered uint32 id, and could be used as a index
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="id">id</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbValue Delete(string tableName, uint id);

        /// <summary>
        /// Search a result in table by key name and key value.
        /// </summary>
        /// <param name="tableName">target table name.</param>
        /// <param name="keyName">target key name.</param>
        /// <param name="keyValue">target key value.</param>
        /// <returns>DbValue of T, The deleted value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue Delete(string tableName, string keyName, object keyValue);

        /// <summary>
        /// Drop the tables.
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Drop(string tableName);

        /// <summary>
        /// Save the changes to the disk immediately.<br/>
        /// When the changes were saved, the transaction could be re-used or ended.
        /// </summary>
        public void SaveChanges();

        /// <summary>
        /// Discard unsaved changes and reset current transaction.
        /// </summary>
        public void Discard();

        /// <summary>
        /// Update a existed data
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="keyName">target key name</param>
        /// <param name="keyValue">target value</param>
        /// <param name="columnName">to be updated column</param>
        /// <param name="value">to be updated value</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update(string tableName, string keyName, object keyValue, string columnName, object value);

        /// <summary>
        /// Update a existed data
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="keyName">target key name</param>
        /// <param name="keyValue">target key value</param>
        /// <param name="value">target instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update<T>(string tableName, string keyName, object keyValue, T value) where T : IDbEntity, new();

        /// <summary>
        /// Update a existed data
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="id">target id</param>
        /// <param name="columnName">to be updated column name</param>
        /// <param name="value">to be updated value</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update(string tableName, uint id, string columnName, object value);
        /// <summary>
        /// Updated a existed data
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="id">default id of target data</param>
        /// <param name="value">to be updated instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update<T>(string tableName, uint id, T value) where T : IDbEntity, new();
        /// <summary>
        /// Updated a existed data
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="condition">condition to search the first matched data</param>
        /// <param name="value">to be updated instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update<T>(string tableName, Func<T, bool> condition, T value) where T : IDbEntity, new();
    }
}