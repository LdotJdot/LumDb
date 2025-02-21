using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Repo;
using LumDbEngine.Element.Value;
using LumDbEngine.Utils.ByteUtils;
using LumDbEngine.Utils.StringUtils;
using System.Diagnostics;

namespace LumDbEngine.Element.Manager
{
    internal class DbManager : IDbManager
    {
        public unsafe IDbResult Create(DbCache db, string tableName, (string columnName, DbValueType type, bool isKey)[] tableHeader)
        {
            if (tableHeader.Length == 0)
            {
                return DbResults.OneColumnAtLeast;
            }
            else if (tableHeader.Length > TablePage.MAX_COLUMN_COUNT)
            {
                return DbResults.ExcessiveNumberOfColumns;
            }

            TableHeaderInfo[] tableHeadersInfo = new TableHeaderInfo[tableHeader.Length];
            HashSet<string> tns = new HashSet<string>(tableHeader.Length);
            for (int i = 0; i < tableHeader.Length; i++)
            {
                LumException.ThrowIfNotTrue(tns.Add(tableHeader[i].columnName),LumExceptionMessage.DuplicateColumnHeader);

#pragma warning disable CA2014 // stack overflow
                byte* keyNameAlloc = stackalloc byte[ColumnHeader.NameLength];

                tableHeader[i].columnName.PaddingToBytes(new Span<byte>(keyNameAlloc, ColumnHeader.NameLength));

                tableHeadersInfo[i].keyName = keyNameAlloc;

                tableHeadersInfo[i].type = tableHeader[i].type;
                tableHeadersInfo[i].isKey = tableHeader[i].isKey;
            }

            Span<byte> bsb = stackalloc byte[RepoNode.KeyLength];
            var keyBytes = tableName.PaddingToBytes(bsb);

            if (db.IsValidPage(db.AvailableTableRepo))
            {
                var result = TableRepoManager.CreateNode(db, new RepoNodeKey(keyBytes), out var node);

                if (result == false)
                {
                    return DbResults.TableAlreadyExisted;
                }

                var tablePage = PageManager.RequestAvailablePage<TablePage>(db);

                TableManager.InitializeTablePage(tablePage, in tableHeadersInfo);

                node.TargetLink.TargetPageID = tablePage.PageId;
                node.Update(db);

                var dataPage = DataManager.InitializeNewDataPage(db, tablePage);

                IndexManager.CreateIndices(db, tablePage, in tableHeadersInfo);
            }

            return DbResults.Success;
        }

        public IDbValues Find(DbCache db, string tableName, Func<IEnumerable<object[]>, IEnumerable<object[]>> condition, bool isBackward)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValues(DbResults.TableNotFound);
            }
            else
            {
                return TableManager.Traversal(db, tablePage, condition,isBackward);
            }
        }

        public IDbValue<uint> Insert(DbCache db, string tableName, TableValue[] values)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue<uint>(DbResults.TableNotFound);
            }

            var id=TableManager.InsertData(db, tablePage, values);

            return new DbValue<uint>(id ?? 0);
        }

        public IDbValue<uint> Insert<T>(DbCache db, string tableName, T t) where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue<uint>(DbResults.TableNotFound);
            }

            var vls = t.Boxing();

            if (vls.Length != tablePage.PageHeader.ColumnCount)
            {
                return new DbValue<uint>(DbResults.DataNumberNotMatchTableColumns);
            }

            var tbd = new TableValue[tablePage.PageHeader.ColumnCount];
            for (int i = 0; i < tablePage.PageHeader.ColumnCount; i++)
            {
                tbd[i] = (tablePage.ColumnHeaders[i].Name.TransformToToString(), vls[i]);
            }

            var id = TableManager.InsertData(db, tablePage, tbd);

            return new DbValue<uint>(id ?? 0);
        }

        public IDbValues<T> Find<T>(DbCache db, string tableName, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward) where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValues<T>(DbResults.TableNotFound);
            }

            return TableManager.Traversal<T>(db, tablePage, condition,isBackward);
        }

        public IDbValue Find(DbCache db, string tableName, string keyName, object keyValue)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue(DbResults.TableNotFound);
            }

            return TableManager.Pick(db, tablePage, keyName, keyValue);
        }

        public IDbValue Delete(DbCache db, string tableName, uint id)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue(DbResults.TableNotFound);
            }

            return TableManager.Delete(db, tablePage, id);
        }

        public IDbValue Delete(DbCache db, string tableName, string keyName, object keyValue)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue(DbResults.TableNotFound);
            }

            return TableManager.Delete(db, tablePage, keyName, keyValue);
        }

        public IDbValue Find(DbCache db, string tableName, uint id)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue(DbResults.TableNotFound);
            }

            return TableManager.Pick(db, tablePage, id);
        }

        public IDbValue<T> Find<T>(DbCache db, string tableName, string keyName, object keyValue) where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue<T>(DbResults.TableNotFound);
            }
            return TableManager.Pick<T>(db, tablePage, keyName, keyValue);
        }

        public IDbValue<T> FindById<T>(DbCache db, string tableName, uint id) where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue<T>(DbResults.TableNotFound);
            }
            return TableManager.Pick<T>(db, tablePage, id);
        }

        public IDbResult Update<T>(DbCache db, string tableName, T value, Func<T, bool> condition) where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            var dataNode = TableManager.FirstOrDefaultNode<T>(db, tablePage, condition);
            if (dataNode == null)
            {
                return DbResults.ConditionNotMatched;
            }

            TableManager.Update(db, tablePage, dataNode, value.Boxing());

            return DbResults.Success;
        }

        public IDbResult Update<T>(DbCache db, string tableName, uint id, T value) where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, id);

            if (dataNode == null)
            {
                return DbResults.ConditionNotMatched;
            }

            TableManager.Update(db, tablePage, dataNode, value.Boxing());

            return DbResults.Success;
        }

        public IDbResult Update(DbCache db, string tableName, uint id, string columnName, object value)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            Debug.Assert(tablePage != null);

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, id);

            if (dataNode == null)
            {
                return DbResults.DataNotFound;
            }

            TableManager.Update(db, tablePage, dataNode, columnName, value);

            return DbResults.Success;
        }

        public IDbResult Update(DbCache db, string tableName, string keyName, object keyValue, string columnName, object value)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, keyName, keyValue);

            if (dataNode == null)
            {
                return DbResults.DataNotFound;
            }

            TableManager.Update(db, tablePage, dataNode, columnName, value);

            return DbResults.Success;
        }

        public IDbResult Update<T>(DbCache db, string tableName, string keyName, object keyValue, T value) where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, keyName, keyValue);

            if (dataNode == null)
            {
                return DbResults.DataNotFound;
            }

            TableManager.Update(db, tablePage, dataNode, value.Boxing());

            return DbResults.Success;
        }

        public IDbResult Drop(DbCache db, string tableName)
        {
            var node = TableRepoManager.FindTableRepoNode(db, tableName);
            if (node == null)
            {
                return DbResults.TableNotFound;
            }

            var tablePage = PageManager.GetPage<TablePage>(db, node.Value.TargetLink.TargetPageID);

            Debug.Assert(tablePage != null);

            TableManager.Drop(db, tablePage);
            var dropNode = node.Value;
            TableRepoManager.Drop(db, ref dropNode);
            return DbResults.Success;
        }


        public IDbValues<T> Where<T>(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[]? conditions, bool isBackward, uint skip, uint limit) where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValues<T>(DbResults.TableNotFound);
            }
            return TableManager.Where<T>(db, tablePage, conditions, isBackward, skip, limit);
        }

        public IDbValues Where(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[]? conditions, bool isBackward, uint skip, uint limit)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValues(DbResults.TableNotFound);
            }
            return TableManager.Where(db, tablePage, conditions,isBackward,skip,limit);
        }

        public IDbValue Count(DbCache db, string tableName, (string keyName, Func<object, bool> checkFunc)[] conditions)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue(DbResults.TableNotFound);
            }
            return TableManager.CountCondition(db, tablePage,conditions);
        }

        public void GoThrough<T>(DbCache db, string tableName, Func<T, bool> action) where T : IDbEntity, new()
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage != null)
            {
                TableManager.GoThrough<T>(db, tablePage, action);
            }

        }
        
        public void GoThrough(DbCache db, string tableName, Func<object[], bool> action)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage != null)
            {
                TableManager.GoThrough(db, tablePage, action);
            }

        }

    }
}