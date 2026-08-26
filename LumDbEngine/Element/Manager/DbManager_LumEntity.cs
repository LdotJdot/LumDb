using LumDbEngine;
using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Table;
using LumDbEngine.Utils.StringUtils;

namespace LumDbEngine.Element.Manager
{
    internal partial class DbManager
    {
        public IDbResult CreateEntity<T>(DbCache db, string tableName) where T : ILumEntity<T>, new()
        {
            return Create(db, tableName, T.DbSchema);
        }

        public IDbValue<uint> InsertEntity<T>(DbCache db, string tableName, T value) where T : ILumEntity<T>, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return new DbValue<uint>(DbResults.TableNotFound);

            var schema = T.DbSchema;
            var cells = new DbCell[schema.Length];
            var writer = new RowWriter(cells);
            T.WriteTo(ref writer, in value);

            var tbd = new TableValue[schema.Length];
            for (int i = 0; i < schema.Length; i++)
            {
                tbd[i] = (schema[i].columnName, cells[i].WithColumnType(schema[i].type));
            }

            var id = TableManager.InsertData(db, tablePage, tbd);
            return new DbValue<uint>(id ?? 0);
        }

        public IDbValue<T> FindEntityById<T>(DbCache db, string tableName, uint id) where T : ILumEntity<T>, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return new DbValue<T>(DbResults.TableNotFound);

            var node = TableManager.FirstOrDefaultNode(db, tablePage, id);
            if (node == null)
                return new DbValue<T>(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, id: {id}"));

            var row = DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, node.Data);
            if (!T.TryReadFrom(row, out var entity))
                return new DbValue<T>(LumException.Raise(LumExceptionMessage.FailedToReadEntity));

            T.SetDbId(ref entity, node.Id);
            return new DbValue<T>(entity);
        }

        public IDbValue<T> FindEntity<T>(DbCache db, string tableName, string keyName, DbCell keyValue) where T : ILumEntity<T>, new()
        {
            var found = Find(db, tableName, keyName, keyValue);
            if (!found.IsSuccess)
                return new DbValue<T>((DbResult)found);

            if (!T.TryReadFrom(found.Row, out var entity))
                return new DbValue<T>(LumException.Raise(LumExceptionMessage.FailedToReadEntity));

            var tablePage = TableRepoManager.GetTablePage(db, tableName)!;
            var node = TableManager.FirstOrDefaultNode(db, tablePage, keyName, keyValue);
            if (node != null)
                T.SetDbId(ref entity, node.Id);

            return new DbValue<T>(entity);
        }

        public IDbResult UpdateEntity<T>(DbCache db, string tableName, uint id, T value) where T : ILumEntity<T>, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return DbResults.TableNotFound;

            var node = TableManager.FirstOrDefaultNode(db, tablePage, id);
            if (node == null)
                return DbResults.DataNotFound;

            var schema = T.DbSchema;
            var cells = new DbCell[schema.Length];
            var writer = new RowWriter(cells);
            T.WriteTo(ref writer, in value);
            for (int i = 0; i < schema.Length; i++)
                cells[i] = cells[i].WithColumnType(schema[i].type);

            TableManager.Update(db, tablePage, node, cells);
            return DbResults.Success;
        }

        public IDbResult UpdateEntity<T>(DbCache db, string tableName, string keyName, DbCell keyValue, T value) where T : ILumEntity<T>, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return DbResults.TableNotFound;

            var node = TableManager.FirstOrDefaultNode(db, tablePage, keyName, keyValue);
            if (node == null)
                return DbResults.DataNotFound;

            var schema = T.DbSchema;
            var cells = new DbCell[schema.Length];
            var writer = new RowWriter(cells);
            T.WriteTo(ref writer, in value);
            for (int i = 0; i < schema.Length; i++)
                cells[i] = cells[i].WithColumnType(schema[i].type);

            TableManager.Update(db, tablePage, node, cells);
            return DbResults.Success;
        }

        public IDbValues<T> FindEntityWhere<T>(DbCache db, string tableName, RowViewPredicate predicate, uint skip, uint limit)
            where T : ILumEntity<T>, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return new DbValues<T>(DbResults.TableNotFound);

            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return new DbValues<T>([]);

            var rootPage = PageManager.GetPage<Element.Structure.Page.Data.DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var list = new List<T>();
            uint skipped = 0;
            uint taken = 0;

            DataManager.GoThrough(db, tablePage.ColumnHeaders, rootPage, (uint id, ref RowView view) =>
            {
                if (!predicate(ref view))
                    return true;

                if (skipped < skip)
                {
                    skipped++;
                    return true;
                }

                if (taken >= limit)
                    return false;

                // Materialize from live view via row buffer copy of current node bytes
                var node = TableManager.FirstOrDefaultNode(db, tablePage, id);
                if (node == null)
                    return true;

                var row = DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, node.Data);
                if (T.TryReadFrom(row, out var entity))
                {
                    T.SetDbId(ref entity, id);
                    list.Add(entity);
                    taken++;
                }

                return taken < limit;
            });

            return new DbValues<T>(list);
        }

        public IDbValue<uint> CountWhere(DbCache db, string tableName, RowViewPredicate predicate)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);
            if (tablePage == null)
                return new DbValue<uint>(DbResults.TableNotFound);

            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return new DbValue<uint>(0);

            var rootPage = PageManager.GetPage<Element.Structure.Page.Data.DataPage>(db, tablePage.PageHeader.RootDataPageId);
            uint count = 0;
            DataManager.GoThrough(db, tablePage.ColumnHeaders, rootPage, (ref RowView view) =>
            {
                if (predicate(ref view))
                    count++;
                return true;
            });
            return new DbValue<uint>(count);
        }
    }
}
