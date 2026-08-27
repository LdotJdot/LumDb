using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Query;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;
using System.Linq.Expressions;

namespace LumDbEngine.Element.Manager
{
    internal partial interface IDbManager
    {
        IDbValue<uint> Insert_Entity<Entity>(DbCache db, string tableName, Entity t) where Entity : IDbEntity, new();

        IDbValue<Entity> Find_Entity<Entity>(DbCache db, string tableName, string keyName, DbCell keyValue) where Entity : IDbEntity, new();

        IDbValue<Entity> FindById_Entity<Entity>(DbCache db, string tableName, uint id) where Entity : IDbEntity, new();

        IDbResult Update_Entity<Entity>(DbCache db, string tableName, Entity value, Expression<Func<Entity, bool>> condition) where Entity : IDbEntity, new();

        IDbResult Update_Entity<Entity>(DbCache db, string tableName, uint id, Entity value) where Entity : IDbEntity, new();

        IDbResult Update_Entity<Entity>(DbCache db, string tableName, string keyName, DbCell keyValue, Entity value) where Entity : IDbEntity, new();

        void GoThrough_Entity<Entity>(DbCache db, string tableName, Func<Entity, bool> action) where Entity : IDbEntity, new();

        IDbValues<T> QueryDbEntity<T>(DbCache db, string tableName, Func<EntityColumnMap, EntityQueryPlan> build)
            where T : IDbEntity, new();

        int CountDbEntity<T>(DbCache db, string tableName, Func<EntityColumnMap, RowExpr?> buildWhere)
            where T : IDbEntity, new();

        bool ExistsDbEntity<T>(DbCache db, string tableName, Func<EntityColumnMap, RowExpr?> buildWhere, bool backward)
            where T : IDbEntity, new();
    }
}
