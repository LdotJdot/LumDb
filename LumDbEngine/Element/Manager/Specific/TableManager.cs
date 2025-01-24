using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Table;
using LumDbEngine.Element.Value;
using LumDbEngine.Utils.StringUtils;
using System.Text;

namespace LumDbEngine.Element.Manager.Specific
{
    internal static class TableManager
    {
        public static unsafe void InitializeTablePage(TablePage page, in TableHeaderInfo[] tableHeaders)
        {
            page.SetColumnCount((byte)(tableHeaders.Length));
            var columnHeaders = new ColumnHeader[page.PageHeader.ColumnCount];

            uint dataSize = 0;
            for (int colId = 0; colId < page.PageHeader.ColumnCount; colId++)
            {
                var inputHeader = tableHeaders[colId];
                var tableHeader = new ColumnHeader(page);
                tableHeader.IsKey = inputHeader.isKey;
                tableHeader.ValueType = inputHeader.type;
                if (tableHeader.IsKey)
                {
                    LumException.ThrowIfNotTrue(tableHeader.ValueType.IsValidFix32(), $"{LumExceptionMessage.DataTypeNotSupport}: {tableHeader.ValueType}");
                }
                new Span<byte>(inputHeader.keyName, ColumnHeader.NameLength).CopyTo(tableHeader.Name);
                columnHeaders[colId] = tableHeader;
                dataSize += (uint)tableHeader.ValueType.GetLength() + DataNode.HEADER_SIZE;
            }

            page.SetColumnHeaders(columnHeaders);
            page.SetDataLength(dataSize);
        }

        public static uint? InsertData(DbCache db, TablePage tablePage, in TableValue[] values)
        {
            LumException.ThrowIfTrue(values.Length > tablePage.PageHeader.ColumnCount, LumExceptionMessage.TooMuchValuesForColumns);

            var dataPage = DataManager.RequestAvailableDataPage(db, tablePage);
            dataPage.MarkDirty();
            dataPage.CurrentDataCount++;
            // mem leak

            var dataNode = DataManager.InsertValueToDataPage(db, tablePage, dataPage, values);

            IndexManager.InsertMainIndex(db, tablePage, dataNode);

            IndexManager.InsertSubIndices(db, tablePage, dataNode, values);
            return dataNode?.Id;
        }

        public static IDbValues Traversal(DbCache db, TablePage tablePage, Func<IEnumerable<object[]>, IEnumerable<object[]>> condition, bool isBackward)
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return new DbValues([]);
            }
            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var values =isBackward? condition(DataManager.GetValues_Backward(db, tablePage.ColumnHeaders, rootPage!).Select(o => o.data)) : condition(DataManager.GetValues(db, tablePage.ColumnHeaders, rootPage!).Select(o => o.data));

            return new DbValues(values);

        }

        public static IDbValues<T> Traversal<T>(DbCache db, TablePage tablePage, Func<IEnumerable<T>, IEnumerable<T>> condition, bool isBackward) where T : IDbEntity, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return new DbValues<T>([]);
            }

            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var values =isBackward? DataManager.GetValuesWithId_Backward(db, tablePage.ColumnHeaders, rootPage!) : DataManager.GetValuesWithId(db, tablePage.ColumnHeaders, rootPage!);
            return new DbValues<T>(condition(values.Select(o => (T)(new T()).UnboxingWithId(o.id, o.obj))));
        }

        public static DataNode? FirstOrDefaultNode<T>(DbCache db, TablePage tablePage, Func<T, bool> condition) where T : IDbEntity, new()
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return null;
            }

            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var values = DataManager.GetValues(db, tablePage.ColumnHeaders, rootPage!);
            var t = new T();

            foreach (var tarGet in values)
            {
                t.Unboxing(tarGet.data);
                if (condition(t))
                {
                    return tarGet.node;
                }
            }
            return null;
        }

        public static DataNode? FirstOrDefaultNode(DbCache db, TablePage tablePage, string columnName, object value)
        {
            var headerIndex = tablePage.GetTableHeaderIndex(columnName);
            var columnHeader = tablePage.ColumnHeaders[headerIndex];
            if (columnHeader.IsKey == false)
            {
                return null;
            }

            if (!columnHeader.ValueType.CheckType(value) || !columnHeader.ValueType.IsValidFix32())
            {
                return null;
            }

            var len = columnHeader.ValueType.GetLength();
            Span<byte> buffer = stackalloc byte[len];
            value.SerializeObjectToBytes(buffer);
            return IndexManager.GetDataByIndex(db, tablePage, NodeManager.GetIndexNode(db, columnHeader.RootSubIndexNode.TargetPageID, columnHeader.RootSubIndexNode.TargetNodeIndex).Value, buffer);
        }

        public static DataNode? FirstOrDefaultNode(DbCache db, TablePage tablePage, uint id)
        {
            Span<byte> key = stackalloc byte[4];
            id.SerializeObjectToBytes(key);
            return IndexManager.GetDataByIndex(db, tablePage, NodeManager.GetIndexNode(db, tablePage.PageHeader.RootIndexNode.TargetPageID, tablePage.PageHeader.RootIndexNode.TargetNodeIndex).Value, key);
        }

        public static IDbValue<T> Pick<T>(DbCache db, TablePage tablePage, uint id) where T : IDbEntity, new()
        {
            var node = FirstOrDefaultNode(db, tablePage, id);

            if (node == null)
            {
                return new DbValue<T>(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, id: {id}"));
            }
            else
            {
                return new DbValue<T>((T)new T().UnboxingWithId(node.Id, DataManager.GetValue(db, tablePage.ColumnHeaders, node.Data)));
            }
        }

        public static IDbValue<T> Pick<T>(DbCache db, TablePage tablePage, string keyName, object keyValue) where T : IDbEntity, new()
        {
            var headerIndex = tablePage.GetTableHeaderIndex(keyName);
            var columnHeader = tablePage.ColumnHeaders[headerIndex];

            if (columnHeader.IsKey == false)
            {
                return new DbValue<T>(LumException.Raise($"{keyName} {LumExceptionMessage.NotKey}"));
            }

            if (!columnHeader.ValueType.CheckType(keyValue) || !columnHeader.ValueType.IsValidFix32())
            {
                return new DbValue<T>(LumException.Raise($"{LumExceptionMessage.DataTypeNotSupport}: {columnHeader.ValueType}"));
            }

            var len = columnHeader.ValueType.GetLength();
            Span<byte> buffer = stackalloc byte[len];
            keyValue.SerializeObjectToBytes(buffer);
            var node = IndexManager.GetDataByIndex(db, tablePage, NodeManager.GetIndexNode(db, columnHeader.RootSubIndexNode.TargetPageID, columnHeader.RootSubIndexNode.TargetNodeIndex).Value, buffer);

            if (node == null)
            {
                return new DbValue<T>(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, {keyName}: {keyValue}"));
            }
            else
            {
                return new DbValue<T>((T)new T().UnboxingWithId(node.Id, DataManager.GetValue(db, tablePage.ColumnHeaders, node.Data)));
            }
        }

        public static IDbValue Pick(DbCache db, TablePage tablePage, uint id)
        {
            Span<byte> key = stackalloc byte[4];
            id.SerializeObjectToBytes(key);
            var node = FirstOrDefaultNode(db, tablePage, id);

            if (node == null)
            {
                return new DbValue(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, id: {id}"));
            }
            else
            {
                return new DbValue(DataManager.GetValue(db, tablePage.ColumnHeaders, node.Data));
            }
        }

        public static IDbValue Pick(DbCache db, TablePage tablePage, string keyName, object keyValue)
        {
            var headerIndex = tablePage.GetTableHeaderIndex(keyName);
            var columnHeader = tablePage.ColumnHeaders[headerIndex];

            if (columnHeader.IsKey == false)
            {
                return new DbValue(LumException.Raise($"{keyName} {LumExceptionMessage.NotKey}"));
            }

            if (!columnHeader.ValueType.CheckType(keyValue) || !columnHeader.ValueType.IsValidFix32())
            {
                return new DbValue(LumException.Raise($"{LumExceptionMessage.DataTypeNotSupport}: {columnHeader.ValueType}"));
            }

            var len = columnHeader.ValueType.GetLength();
            Span<byte> buffer = stackalloc byte[len];
            keyValue.SerializeObjectToBytes(buffer);
            var node = IndexManager.GetDataByIndex(db, tablePage, NodeManager.GetIndexNode(db, columnHeader.RootSubIndexNode.TargetPageID, columnHeader.RootSubIndexNode.TargetNodeIndex).Value, buffer);

            if (node == null)
            {
                return new DbValue(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, {keyName}: {keyValue}"));
            }
            else
            {
                return new DbValue(DataManager.GetValue(db, tablePage.ColumnHeaders, node.Data));
            }
        }

        private static IDbValue Delete(DbCache db, TablePage tablePage, DataNode? dataNode)
        {
            if (dataNode == null)
            {
                return new DbValue(LumException.Raise(LumExceptionMessage.DataNoFound));
            }
            else
            {
                var dbResult = new DbValue(DataManager.GetValue(db, tablePage.ColumnHeaders, dataNode.Data));
                {
                    DataManager.DeleteDataNodeByIndex(db, tablePage, dataNode);
                    IndexManager.DeleteMainIndex(db, tablePage, dataNode);
                    IndexManager.DeleteSubIndices(db, tablePage, dataNode);
                }
                return dbResult;
            }
        }

        public static IDbValue Delete(DbCache db, TablePage tablePage, uint id)
        {
            var dataNode = FirstOrDefaultNode(db, tablePage, id);
            return Delete(db, tablePage, dataNode);
        }

        public static IDbValue Delete(DbCache db, TablePage tablePage, string keyName, object keyValue)
        {
            var dataNode = FirstOrDefaultNode(db, tablePage, keyName, keyValue);
            return Delete(db, tablePage, dataNode);
        }

        internal static void Update(DbCache db, TablePage tablePage, DataNode dataNode, string columnName, object value)
        {
            var headerIndex = tablePage.GetTableHeaderIndex(columnName);
            var header = tablePage.ColumnHeaders[headerIndex];

            var valueSpan = dataNode.Data.Slice(DataManager.GetDataOffset(tablePage.ColumnHeaders, headerIndex), header.ValueType.GetLength());

            object origin = valueSpan.DeserializeBytesToValue(db, header.ValueType);

            if (!Equals(value, origin))
            {
                var oldKey = valueSpan.ToArray();

                DataManager.UpdateSingleData(db, header, dataNode, value, headerIndex);

                if (header.IsKey)
                {
                    IndexManager.UpdateIndex(db, tablePage, dataNode, header, header.Name, oldKey);
                }
            }
        }

        internal static void Update(DbCache db, TablePage tablePage, DataNode dataNode, object[] values)
        {
            for (int i = 0; i < tablePage.ColumnHeaders.Length; i++)
            {
                if (values[i] == null)
                {
                    continue;
                }

                var header = tablePage.ColumnHeaders[i];
                var valueSpan = dataNode.Data.Slice(DataManager.GetDataOffset(tablePage.ColumnHeaders, i), header.ValueType.GetLength());

                object origin = valueSpan.DeserializeBytesToValue(db, header.ValueType);

                if (!Equals(values[i], origin))
                {
                    var oldKey = valueSpan.ToArray();

                    DataManager.UpdateData(db, tablePage, dataNode, values, i);

                    if (header.IsKey)
                    {
                        IndexManager.UpdateIndex(db, tablePage, dataNode, header, header.Name, oldKey);
                    }
                }
            }
        }

        internal static void Drop(DbCache db, TablePage tablePage)
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return;
            }

            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            rootPage.MarkDirty();

            HashSet<uint> pages = new HashSet<uint>(64)
            {
                tablePage.PageId
            };

            foreach (var header in tablePage.ColumnHeaders)
            {
                if (header.IsKey)
                {
                    var rootKeyNode = NodeManager.GetIndexNode(db, header.RootSubIndexNode.TargetPageID, header.RootSubIndexNode.TargetNodeIndex);
                    IndexManager.GetIndexPages(db, rootKeyNode, pages);
                }
            }

            var rootIdNode = NodeManager.GetIndexNode(db, tablePage.PageHeader.RootIndexNode.TargetPageID, tablePage.PageHeader.RootIndexNode.TargetNodeIndex);

            IndexManager.GetIndexPages(db, rootIdNode, pages);

            DataManager.GetDataPagesAndRemoveDataVar(db, tablePage, rootPage, pages);

            PageManager.DropPages(db, pages);
        }


        public static IDbValues TraversalWithCondition(DbCache db, TablePage tablePage, (string keyName, Func<object, bool> checkFunc)[] conditions, bool isBackward)
        {
            return null;
            //if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            //{
            //    return new DbValues(0, []);
            //}
            //var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            //var values = isBackward ? condition(DataManager.GetValues_Backward(db, tablePage.ColumnHeaders, rootPage!).Select(o => o.data)) : condition(DataManager.GetValues(db, tablePage.ColumnHeaders, rootPage!).Select(o => o.data));

            //return new DbValues(tablePage.PageHeader.ColumnCount, values);

        }

        public static IDbValues<T> TraversalWithCondition<T>(DbCache db, TablePage tablePage, (string keyName, Func<object, bool> checkFunc)[] conditions, bool isBackward) where T : IDbEntity, new()
        {
            return null;
            //if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            //{
            //    return new DbValues<T>([]);
            //}

            //var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            //var values = isBackward ? DataManager.GetValuesWithId_Backward(db, tablePage.ColumnHeaders, rootPage!) : DataManager.GetValuesWithId(db, tablePage.ColumnHeaders, rootPage!);
            //return new DbValues<T>(condition(values.Select(o => (T)(new T()).UnboxingWithId(o.id, o.obj))));
        }

        public static unsafe IDbValue CountCondition(DbCache db, TablePage tablePage, (string keyName, Func<object, bool> checkFunc)[] conditions )
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                return new DbValue([0]);
            }

            var fullCondition = new Func<object, bool>?[tablePage.ColumnCount];

            var headerStringNames=tablePage.ColumnHeaders.Select(o=> Encoding.UTF8.GetString(o.Name).TrimEnd('\0')).ToArray();
            var keyStringNames=conditions.Select(o=>o.keyName).ToArray();

            // not done

            for (int i = 0; i < fullCondition.Length; i++)
            {
                for(int j = 0; j < conditions.Length; j++)
                {
                    if (keyStringNames[j].Equals(headerStringNames[i],StringComparison.Ordinal))
                    {
                        fullCondition[i] = conditions[j].checkFunc;
                        keyStringNames[j] = string.Empty;
                        break;
                    }
                }
            }


            var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var value = DataManager.CountWithCnditions(db, tablePage.ColumnHeaders, fullCondition, rootPage!);
            return new DbValue([value]);
        }
    }
}