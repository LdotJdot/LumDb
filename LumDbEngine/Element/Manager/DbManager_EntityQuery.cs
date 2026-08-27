using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Query;
using LumDbEngine.Element.Structure;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Manager
{
    internal partial class DbManager
    {
        public IDbValues<T> QueryLumEntity<T>(DbCache db, string tableName, Func<EntityColumnMap, EntityQueryPlan> build)
            where T : ILumEntity<T>, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return new DbValues<T>(DbResults.TableNotFound);

            var map = EntityColumnMap.FromHeaders(tablePage.ColumnHeaders);
            var plan = build(map);
            return EntityQueryExecutor.Execute(
                db, tablePage, plan,
                (id, row) =>
                {
                    if (!T.TryReadFrom(row, out var entity))
                        throw LumException.Raise(LumExceptionMessage.FailedToReadEntity);
                    T.SetDbId(ref entity, id);
                    return entity;
                },
                id =>
                {
                    var found = FindEntityById<T>(db, tableName, id);
                    return found.IsSuccess ? found.Value : default;
                });
        }

        public int CountLumEntity<T>(DbCache db, string tableName, Func<EntityColumnMap, RowExpr?> buildWhere)
            where T : ILumEntity<T>, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return 0;
            var map = EntityColumnMap.FromHeaders(tablePage.ColumnHeaders);
            return (int)EntityQueryExecutor.Count(db, tablePage, buildWhere(map));
        }

        public bool ExistsLumEntity<T>(DbCache db, string tableName, Func<EntityColumnMap, RowExpr?> buildWhere, bool backward)
            where T : ILumEntity<T>, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return false;
            var map = EntityColumnMap.FromHeaders(tablePage.ColumnHeaders);
            return EntityQueryExecutor.Exists(db, tablePage, buildWhere(map), backward);
        }

        public IDbValues<T> QueryDbEntity<T>(DbCache db, string tableName, Func<EntityColumnMap, EntityQueryPlan> build)
            where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return new DbValues<T>(DbResults.TableNotFound);

            var map = EntityColumnMap.FromHeaders(tablePage.ColumnHeaders);
            var plan = build(map);
            return EntityQueryExecutor.Execute(
                db, tablePage, plan,
                (id, row) => (T)new T().UnboxingWithId(id, row),
                id =>
                {
                    var found = FindById_Entity<T>(db, tableName, id);
                    return found.IsSuccess ? found.Value : default;
                });
        }

        public int CountDbEntity<T>(DbCache db, string tableName, Func<EntityColumnMap, RowExpr?> buildWhere)
            where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return 0;
            var map = EntityColumnMap.FromHeaders(tablePage.ColumnHeaders);
            return (int)EntityQueryExecutor.Count(db, tablePage, buildWhere(map));
        }

        public bool ExistsDbEntity<T>(DbCache db, string tableName, Func<EntityColumnMap, RowExpr?> buildWhere, bool backward)
            where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return false;
            var map = EntityColumnMap.FromHeaders(tablePage.ColumnHeaders);
            return EntityQueryExecutor.Exists(db, tablePage, buildWhere(map), backward);
        }

        public int DeleteByPlan(DbCache db, string tableName, Func<EntityColumnMap, EntityQueryPlan> build)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return 0;

            var map = EntityColumnMap.FromHeaders(tablePage.ColumnHeaders);
            var ids = EntityQueryExecutor.CollectIds(db, tablePage, build(map));
            var n = 0;
            foreach (var id in ids)
            {
                if (TableManager.Delete(db, tablePage, id).IsSuccess)
                    n++;
            }

            return n;
        }
    }
}
