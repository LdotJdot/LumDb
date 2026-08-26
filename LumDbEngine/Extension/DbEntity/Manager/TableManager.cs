using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Table;
using LumDbEngine.Extension.DbEntity;

namespace LumDbEngine.Element.Manager.Specific
{
    internal static partial class TableManager
    {
        public static IDbValues<Entity> Traversal_Entity<Entity>(DbCache db, TablePage tablePage, Func<IEnumerable<Entity>, IEnumerable<Entity>> condition, bool isBackward)
            where Entity : IDbEntity, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return new DbValues<Entity>([]);

            var list = new List<Entity>();
            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            DataManager.GoThrough(db, tablePage.ColumnHeaders, rootPage, (uint id, ref RowView _) =>
            {
                var node = FirstOrDefaultNode(db, tablePage, id);
                if (node == null)
                    return true;
                var row = DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, node.Data);
                var entity = new Entity();
                entity.UnboxingWithId(id, row);
                list.Add(entity);
                return true;
            });

            // isBackward: reverse list for simple parity with prior behavior
            if (isBackward)
                list.Reverse();

            return new DbValues<Entity>(condition(list));
        }

        public static DataNode? FirstOrDefaultNode_Entity<Entity>(DbCache db, TablePage tablePage, Func<Entity, bool> condition)
            where Entity : IDbEntity, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return null;

            DataNode? hit = null;
            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            DataManager.GoThrough(db, tablePage.ColumnHeaders, rootPage, (uint id, ref RowView _) =>
            {
                var node = FirstOrDefaultNode(db, tablePage, id);
                if (node == null)
                    return true;
                var row = DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, node.Data);
                var entity = new Entity();
                entity.UnboxingWithId(id, row);
                if (condition(entity))
                {
                    hit = node;
                    return false;
                }
                return true;
            });
            return hit;
        }

        public static IDbValue<Entity> Pick_Entity<Entity>(DbCache db, TablePage tablePage, uint id) where Entity : IDbEntity, new()
        {
            var node = FirstOrDefaultNode(db, tablePage, id);
            if (node == null)
                return new DbValue<Entity>(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, id: {id}"));

            var row = DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, node.Data);
            return new DbValue<Entity>((Entity)new Entity().UnboxingWithId(node.Id, row));
        }

        public static IDbValue<Entity> Pick_Entity<Entity>(DbCache db, TablePage tablePage, string keyName, DbCell keyValue)
            where Entity : IDbEntity, new()
        {
            var headerIndex = tablePage.GetTableHeaderIndex(keyName);
            var columnHeader = tablePage.ColumnHeaders[headerIndex];

            if (columnHeader.IsKey == false)
                return new DbValue<Entity>(LumException.Raise($"{keyName} {LumExceptionMessage.NotKey}"));

            if (!columnHeader.ValueType.IsValidFix32())
                return new DbValue<Entity>(LumException.Raise($"{LumExceptionMessage.DataTypeNotSupport}: {columnHeader.ValueType}"));

            var node = FirstOrDefaultNode(db, tablePage, keyName, keyValue);
            if (node == null)
                return new DbValue<Entity>(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, {keyName}"));

            var row = DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, node.Data);
            return new DbValue<Entity>((Entity)new Entity().UnboxingWithId(node.Id, row));
        }

        public static void GoThrough_Entity<Entity>(DbCache db, TablePage tablePage, Func<Entity, bool> action)
            where Entity : IDbEntity, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return;

            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            DataManager.GoThrough_Entity(db, tablePage.ColumnHeaders, rootPage!, action);
        }
    }
}
