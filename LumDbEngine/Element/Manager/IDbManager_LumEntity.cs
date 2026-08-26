using LumDbEngine;
using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Manager
{
    internal partial interface IDbManager
    {
        IDbResult CreateEntity<T>(DbCache db, string tableName) where T : ILumEntity<T>, new();

        IDbValue<uint> InsertEntity<T>(DbCache db, string tableName, T value) where T : ILumEntity<T>, new();

        IDbValue<T> FindEntityById<T>(DbCache db, string tableName, uint id) where T : ILumEntity<T>, new();

        IDbValue<T> FindEntity<T>(DbCache db, string tableName, string keyName, DbCell keyValue) where T : ILumEntity<T>, new();

        IDbResult UpdateEntity<T>(DbCache db, string tableName, uint id, T value) where T : ILumEntity<T>, new();

        IDbResult UpdateEntity<T>(DbCache db, string tableName, string keyName, DbCell keyValue, T value) where T : ILumEntity<T>, new();

        IDbValues<T> FindEntityWhere<T>(DbCache db, string tableName, RowViewPredicate predicate, uint skip, uint limit)
            where T : ILumEntity<T>, new();

        IDbValue<uint> CountWhere(DbCache db, string tableName, RowViewPredicate predicate);
    }
}
