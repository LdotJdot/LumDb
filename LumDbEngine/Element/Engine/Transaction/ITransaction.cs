using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction
{
    /// <summary>
    /// The transaction derived from db engine.<br/>
    /// This kind of transaction ensures thread safety by using a regular mutex lock, which guarantees
    /// that only one thread can execute the function at a time. This ensures data consistency
    /// but may limit performance due to its sequential nature.<br/>
    /// The readonly transaction <see cref="ITransactionReadonly"/> is recommended for use in read-only scenarios to maximize performance.
    /// </summary>
    public partial interface ITransaction : IDisposable
    {
        /// <summary>
        /// Transaction unique id.
        /// </summary>
        public Guid Id { get; }

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
        /// <returns>The id of the inserted data. The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<uint> Insert(string tableName, (string columnName, object value)[] values);


      

        /// <summary>
        /// Search the results in table
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="condition">the match condition of the iterate search</param>
        /// <param name="isBackward">execute backward or forward search</param>
        /// <returns>DbValues, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues Find(string tableName, Func<IEnumerable<object[]>, IEnumerable<object[]>> condition, bool isBackward = false);

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
        /// Search the results in table with specific condition
        /// </summary>
        /// <param name="tableName">table name</param>
        /// <param name="conditions">value filter condition</param>
        /// <param name="isBackward">execute backward or forward search</param>
        /// <param name="skip">result skip number</param>
        /// <param name="limit">result limit number</param>
        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues Find(string tableName, bool isBackward , uint skip, uint limit, params (string keyName, Func<object, bool> checkFunc)[] conditions);

            /// <summary>
        /// Search the results in table with specific condition
        /// </summary>
        /// <param name="tableName">table name</param>
        /// <param name="conditions">value filter condition</param>

        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues Find(string tableName, params (string keyName, Func<object, bool> checkFunc)[] conditions);
        
        /// <summary>
        /// Count the results in table with specific condition
        /// </summary>
        /// <param name="tableName">table name</param>
        /// <param name="conditions">value filter condition</param>   
        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue Count(string tableName, (string keyName, Func<object, bool> checkFunc)[] conditions);

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
        /// <param name="tableName">target table name</param>
        /// <param name="id">target id</param>
        /// <param name="columnName">to be updated column name</param>
        /// <param name="value">to be updated value</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update(string tableName, uint id, string columnName, object value);
       
    
             /// <summary>
        /// Go through the valid data for special action like sum or count.
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="action">action with respect to data</param>
        public void GoThrough(string tableName, Func<object[], bool> action);


        /// <summary>
        /// Get the all valid tables
        /// </summary>
        /// <returns></returns>
        public IDbValues<(string tableName, (string columnName, string dataType, bool isKey)[] columns)> GetTableNames();


    }
}