using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Repo;
using LumDbEngine.Extension.DbEntity;
using LumDbEngine.Utils.ByteUtils;
using LumDbEngine.Utils.StringUtils;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace LumDbEngine.Element.Manager
{
    internal partial class DbManager : IDbManager
    {
        public IDbValues<(string tableName, (string columnName, string dataType, bool isKey)[])> GetTableNames(DbCache db)
        {
            var nds = TableRepoManager.IterateNodes(db);


            var names = new List<(string tableName,(string columnName,string dataType, bool isKey)[])>(nds.Count);
            for(int i = 0; i < nds.Count; i++)
            {
                if (nds[i].TargetLink.TargetPageID == 0) continue;
                var tp = PageManager.GetPage<TablePage>(db,nds[i].TargetLink.TargetPageID);                
                names.Add((nds[i].KeyToString().TrimEnd('\0'),tp.ColumnHeaders.Select(o=>(o.Name.TransformToToString(),o.ValueType.ToString(),o.IsKey)).ToArray()));
            }

            return new DbValues<(string tableName, (string columnName, string dataType, bool isKey)[])>(names);
        }

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

            if (!db.IsValidPage(db.AvailableTableRepo))
            {
                if (!db.IsValidPage(db.RootPageId))
                    return DbResults.GetFail(LumExceptionMessage.InternalError);

                // The available-repo pointer can be stale while the root repo page is still live.
                db.SetAvailableTableRepoId(db.RootPageId);
            }

            var result = TableRepoManager.CreateNode(db, new RepoNodeKey(keyBytes), out var node);

            if (result == false)
            {
                return DbResults.TableAlreadyExisted;
            }

            var tablePage = PageManager.RequestAvailablePage<TablePage>(db);

            TableManager.InitializeTablePage(tablePage, in tableHeadersInfo);

            node.TargetLink.TargetPageID = tablePage.PageId;
            node.Update(db);

            DataManager.InitializeNewDataPage(db, tablePage);

            IndexManager.CreateIndices(db, tablePage, in tableHeadersInfo);

            return DbResults.Success;
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

        public IDbValue Delete(DbCache db, string tableName, uint id)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue(DbResults.TableNotFound);
            }

            return TableManager.Delete(db, tablePage, id);
        }

        public IDbValue Delete(DbCache db, string tableName, string keyName, DbCell keyValue)
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

        public IDbValue Find(DbCache db, string tableName, string keyName, DbCell keyValue)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return new DbValue(DbResults.TableNotFound);
            }

            return TableManager.Pick(db, tablePage, keyName, keyValue);
        }

        public IDbResult Update(DbCache db, string tableName, uint id, string columnName, DbCell value)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage == null)
            {
                return DbResults.TableNotFound;
            }

            var dataNode = TableManager.FirstOrDefaultNode(db, tablePage, id);

            if (dataNode == null)
            {
                return DbResults.DataNotFound;
            }

            TableManager.Update(db, tablePage, dataNode, columnName, value);

            return DbResults.Success;
        }

        public IDbResult Update(DbCache db, string tableName, string keyName, DbCell keyValue, string columnName, DbCell value)
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

        public void GoThrough(DbCache db, string tableName, RowViewAction action)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage != null)
            {
                TableManager.GoThrough(db, tablePage, action);
            }
        }

        public void GoThrough(DbCache db, string tableName, RowViewIdAction action)
        {
            var tablePage = TableRepoManager.GetTablePage(db, tableName);

            if (tablePage != null)
            {
                TableManager.GoThrough(db, tablePage, action);
            }
        }
    }
}