using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Query;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Table;
using LumDbEngine.Extension.DbEntity;
using LumDbEngine.Utils.StringUtils;
using System.Diagnostics;
using System.Linq.Expressions;

namespace LumDbEngine.Element.Manager
{
    internal partial class DbManager
    {
        public IDbValue<uint> Insert_Entity<Entity>(DbCache db, string tableName, Entity t) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return new DbValue<uint>(DbResults.TableNotFound);

            var cells = new DbCell[tablePage.PageHeader.ColumnCount];
            var writer = new RowWriter(cells);
            t.WriteTo(ref writer);

            var tbd = new TableValue[tablePage.PageHeader.ColumnCount];
            for (int i = 0; i < tablePage.PageHeader.ColumnCount; i++)
            {
                tbd[i] = (tablePage.ColumnHeaders[i].Name.TransformToToString(),
                    cells[i].WithColumnType(tablePage.ColumnHeaders[i].ValueType));
            }

            var id = TableManager.InsertData(db, tablePage, tbd);
            return new DbValue<uint>(id ?? 0);
        }

        public IDbValue<Entity> Find_Entity<Entity>(DbCache db, string tableName, string keyName, DbCell keyValue)
            where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return new DbValue<Entity>(DbResults.TableNotFound);
            return TableManager.Pick_Entity<Entity>(db, tablePage, keyName, keyValue);
        }

        public IDbValue<Entity> FindById_Entity<Entity>(DbCache db, string tableName, uint id) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return new DbValue<Entity>(DbResults.TableNotFound);
            return TableManager.Pick_Entity<Entity>(db, tablePage, id);
        }

        private static DbCell[] EntityToCells(IDbEntity entity, int columnCount)
        {
            var cells = new DbCell[columnCount];
            var writer = new RowWriter(cells);
            entity.WriteTo(ref writer);
            return cells;
        }

        public IDbResult Update_Entity<Entity>(DbCache db, string tableName, Entity value, Expression<Func<Entity, bool>> condition)
            where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return DbResults.TableNotFound;

            var map = EntityColumnMap.FromHeaders(tablePage.ColumnHeaders);
            var where = LinqToRowExpr.Translate(condition, map);
            var id = EntityQueryExecutor.FindFirstId(db, tablePage, where);
            if (id == null)
                return DbResults.DataNotFound;

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, id.Value);
            if (dataNode == null)
                return DbResults.DataNotFound;

            TableManager.Update(db, tablePage, dataNode, EntityToCells(value, tablePage.PageHeader.ColumnCount));
            return DbResults.Success;
        }

        public IDbResult Update_Entity<Entity>(DbCache db, string tableName, uint id, Entity value) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return DbResults.TableNotFound;

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, id);
            if (dataNode == null)
                return DbResults.DataNotFound;

            TableManager.Update(db, tablePage, dataNode, EntityToCells(value, tablePage.PageHeader.ColumnCount));
            return DbResults.Success;
        }

        public IDbResult Update_Entity<Entity>(DbCache db, string tableName, string keyName, DbCell keyValue, Entity value)
            where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return DbResults.TableNotFound;

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, keyName, keyValue);
            if (dataNode == null)
                return DbResults.DataNotFound;

            TableManager.Update(db, tablePage, dataNode, EntityToCells(value, tablePage.PageHeader.ColumnCount));
            return DbResults.Success;
        }

        public void GoThrough_Entity<Entity>(DbCache db, string tableName, Func<Entity, bool> action) where Entity : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage != null)
                TableManager.GoThrough_Entity(db, tablePage, action);
        }
    }
}
