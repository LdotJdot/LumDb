using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Engine.Transaction.AsNoTracking;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;
using System.Diagnostics.CodeAnalysis;

// #if !NATIVE_AOT

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
        /// Create table
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public IDbResult Create
             <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
             (string tableName) where T : class;

        /// <summary>
        /// Insert values to a table
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="t">target instance of the IDbEntity</param>
        /// <returns>The id of the inserted data. The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<uint> Insert
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, T t) where T : class;
                

        /// <summary>
        /// Search a result in table by key name and key value
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="keyName">target key name</param>
        /// <param name="keyValue">target key value</param>
        /// <returns>DbValue of T, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<T> Find
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, string keyName, object keyValue) where T : class, new();

        /// <summary>
        /// Search a result in table by key name and key value  
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="id">target id. <br/>
        /// Each data in table has a unique ordered uint32 id, and could be used as a index</param>
        /// <returns>DbValue of T, The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValue<T> Find
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, uint id) where T : class, new();

        /// <summary>
        /// Search the results in table with specific condition
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">table name</param>
        /// <param name="condition">value filter condition</param>
        /// <param name="isBackward">execute backward or forward search</param>
        /// <param name="skip">result skip number</param>
        /// <param name="limit">result limit number</param>
        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues<T> Find
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, bool isBackward, uint skip, uint limit, Func<T, bool> condition) where T : class, new();

      
        /// <summary>
        /// Search the results in table with specific condition
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">table name</param>
        /// <param name="condition">value filter condition</param>

        /// <returns>DbValues of T. The value is present only when the 'IsSuccess' is 'true'</returns>
        public IDbValues<T> Find
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, Func<T, bool> condition) where T : class, new();

     
        /// <summary>
        /// Update a existed data
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="keyName">target key name</param>
        /// <param name="keyValue">target key value</param>
        /// <param name="value">target instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, string keyName, object keyValue, T value) where T : class, new();

       
        /// <summary>
        /// Updated a existed data
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="id">default id of target data</param>
        /// <param name="value">to be updated instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, uint id, T value) where T : class, new();
        /// <summary>
        /// Updated a existed data
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="condition">condition to search the first matched data</param>
        /// <param name="value">to be updated instance of the IDbEntity</param>
        /// <returns>The action is executed only when the 'IsSuccess' is 'true'</returns>
        public IDbResult Update
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, Func<T, bool> condition, T value) where T : class, new();

        /// <summary>
        /// Go through the valid data for special action like sum or count.
        /// </summary>
        /// <typeparam name="T">A class implement IDbEntity interface corresponding to the table header structure.</typeparam>
        /// <param name="tableName">target table name</param>
        /// <param name="action">action with respect to data</param>
        public void GoThrough
            <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
            (string tableName, Func<T,bool> action) where T : class, new();

    }
}

// #endif
