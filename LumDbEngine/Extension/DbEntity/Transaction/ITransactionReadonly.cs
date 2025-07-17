using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Engine.Transaction.AsNoTracking
{
    /// <summary>
    /// The transaction derived from db engine.<br/>
    /// This function ensures thread safety while allowing multiple threads to execute simultaneously.
    /// It uses read-write locks) to allow
    /// concurrent access to shared resources, improving performance while maintaining data integrity.<br/>
    /// Recommended for use in read-only scenarios to maximize performance.
    /// </summary>
    public partial interface ITransactionReadonly : IDisposable
    {       

        /// <summary>
        /// Search the results in table
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="condition">the match condition of the iterate search.</param>
        /// <param name="isBackward">execute backward or forward search</param>
        /// e.g. o=>o.Where(l=>foo(l.name))</param>
        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues<T> Find_Entity<T>(string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward = false) where T : IDbEntity, new();

        /// <summary>
        /// Search a result in table by key name and key value
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="keyName">target key name</param>
        /// <param name="keyValue">target key value</param>
        /// <returns>DbValue of T, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<T> Find_Entity<T>(string tableName, string keyName, object keyValue) where T : IDbEntity, new();

        /// <summary>
        /// Search a result in table by key name and key value  
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="id">target id. <br/>
        /// Each data in table has a unique ordered uint32 id, and could be used as a index</param>
        /// <returns>DbValue of T, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<T> Find_Entity<T>(string tableName, uint id) where T : IDbEntity, new();

        /// <summary>
        /// Search the results in table with specific condition
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">table name</param>
        /// <param name="conditions">value filter condition</param>
        /// <param name="isBackward">execute backward or forward search</param>
        /// <param name="skip">result skip number</param>
        /// <param name="limit">result limit number</param>
        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues<T> Find_Entity<T>(string tableName, bool isBackward, uint skip, uint limit, params (string keyName, Func<object, bool> checkFunc)[] conditions) where T : IDbEntity, new();

        /// <summary>
        /// Search the results in table with specific condition
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">table name</param>
        /// <param name="conditions">value filter condition</param>

        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues<T> Find_Entity<T>(string tableName, params (string keyName, Func<object, bool> checkFunc)[] conditions) where T : IDbEntity, new();

     
        /// <summary>
        /// Go through the valid data for special action like sum or count.
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="action">action with respect to data</param>
        public void GoThrough_Entity<T>(string tableName, Func<T,bool> action) where T : IDbEntity, new();

    }
}