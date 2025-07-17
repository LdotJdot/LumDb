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
        /// Insert values to a table
        /// </summary>
        /// <typeparam name="Entity">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="t">target instance of the IDbEntity</param>
        /// <returns>The id of the inserted data. The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<uint> Insert_Entity<Entity>(string tableName, Entity t) where Entity : IDbEntity, new();
                

        /// <summary>
        /// Search the results in table
        /// </summary>
        /// <typeparam name="Entity">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="condition">the match condition of the iterate search.</param>
        /// <param name="isBackward">execute backward or forward search</param>
        /// e.g. o=>o.Where(l=>foo(l.name))</param>
        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues<Entity> Find_Entity<Entity>
            (string tableName, Func<IEnumerable<Entity>, IEnumerable<Entity>> condition, bool isBackward = false) 
            where Entity : IDbEntity, new();

        /// <summary>
        /// Search a result in table by key name and key value
        /// </summary>
        /// <typeparam name="Entity">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="keyName">target key name</param>
        /// <param name="keyValue">target key value</param>
        /// <returns>DbValue of T, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<Entity> Find_Entity<Entity>(string tableName, string keyName, object keyValue) where Entity : IDbEntity, new();

        /// <summary>
        /// Search a result in table by key name and key value  
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="id">target id. <br/>
        /// Each data in table has a unique ordered uint32 id, and could be used as a index</param>
        /// <returns>DbValue of T, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<Entity> Find_Entity<Entity>(string tableName, uint id) where Entity : IDbEntity, new();

        /// <summary>
        /// Search the results in table with specific condition
        /// </summary>
        /// <typeparam name="Entity">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">table name</param>
        /// <param name="conditions">value filter condition</param>
        /// <param name="isBackward">execute backward or forward search</param>
        /// <param name="skip">result skip number</param>
        /// <param name="limit">result limit number</param>
        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues<Entity> Find_Entity<Entity>(string tableName, bool isBackward, uint skip, uint limit, params (string keyName, Func<object, bool> checkFunc)[] conditions) where Entity : IDbEntity, new();

      
        /// <summary>
        /// Search the results in table with specific condition
        /// </summary>
        /// <typeparam name="Entity">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">table name</param>
        /// <param name="conditions">value filter condition</param>

        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues<Entity> Find_Entity<Entity>(string tableName, params (string keyName, Func<object, bool> checkFunc)[] conditions) where Entity : IDbEntity, new();

     
        /// <summary>
        /// Update a existed data
        /// </summary>
        /// <typeparam name="Entity">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="keyName">target key name</param>
        /// <param name="keyValue">target key value</param>
        /// <param name="value">target instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update_Entity<Entity>(string tableName, string keyName, object keyValue, Entity value) where Entity : IDbEntity, new();

       
        /// <summary>
        /// Updated a existed data
        /// </summary>
        /// <typeparam name="Entity">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="id">default id of target data</param>
        /// <param name="value">to be updated instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update_Entity<Entity>(string tableName, uint id, Entity value) where Entity : IDbEntity, new();
        /// <summary>
        /// Updated a existed data
        /// </summary>
        /// <typeparam name="Entity">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="condition">condition to search the first matched data</param>
        /// <param name="value">to be updated instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update_Entity<Entity>(string tableName, Func<Entity, bool> condition, Entity value) where Entity : IDbEntity, new();

        /// <summary>
        /// Go through the valid data for special action like sum or count.
        /// </summary>
        /// <typeparam name="Entity">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="action">action with respect to data</param>
        public void GoThrough_Entity<Entity>(string tableName, Func<Entity,bool> action) where Entity : IDbEntity, new();

    }
}