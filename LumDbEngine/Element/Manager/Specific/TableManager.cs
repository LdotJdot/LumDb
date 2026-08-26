using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.Table;
using LumDbEngine.Extension.DbEntity;
using LumDbEngine.Utils.StringUtils;
using System.Text;

namespace LumDbEngine.Element.Manager.Specific
{
    internal  static partial class TableManager
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
            LumException.ThrowIfTrue(values.Length != tablePage.PageHeader.ColumnCount, LumExceptionMessage.ColumnElementNotEqual);            
            
            foreach(var val in values)
            {
                if (!tablePage.IsHeaderExists(val.columnName))
                {
                    throw LumException.Raise($"{LumExceptionMessage.ColumnNameNotExisted}:{val.columnName}");
                }
            }

            var valuesOrdered=values.OrderBy(val => tablePage.GetTableHeaderIndex(val.columnName)).ToArray();

            // Validate fixed lengths / types and uniqueness BEFORE mutating pages (no dirty write on failure).
            ValidateInsertValues(tablePage, valuesOrdered);
            EnsureNoDuplicateBusinessKeys(db, tablePage, valuesOrdered);

            var dataPage = DataManager.RequestAvailableDataPage(db, tablePage);
            dataPage.MarkDirty();
            dataPage.CurrentDataCount++;
            // mem leak

            var dataNode = DataManager.InsertValueToDataPage(db, tablePage, dataPage, valuesOrdered);

            IndexManager.InsertMainIndex(db, tablePage, dataNode);

            IndexManager.InsertSubIndices(db, tablePage, dataNode, valuesOrdered);
            return dataNode?.Id;
        }

        private static void ValidateInsertValues(TablePage tablePage, TableValue[] valuesOrdered)
        {
            for (int i = 0; i < valuesOrdered.Length; i++)
            {
                var header = tablePage.ColumnHeaders[i];
                var cell = valuesOrdered[i].value.WithColumnType(header.ValueType);
                cell.EnsureFitsColumn(header.ValueType);
            }
        }

        private static void EnsureNoDuplicateBusinessKeys(DbCache db, TablePage tablePage, TableValue[] valuesOrdered, uint? excludeDataId = null)
        {
            // Fixed keys are at most 32 bytes (Str32B/Bytes32).
            Span<byte> buffer = stackalloc byte[32];
            for (int i = 0; i < valuesOrdered.Length; i++)
            {
                var header = tablePage.ColumnHeaders[i];
                if (!header.IsKey)
                    continue;

                var cell = valuesOrdered[i].value.WithColumnType(header.ValueType);
                var len = header.ValueType.GetLength();
                var keySpan = buffer.Slice(0, len);
                cell.Serialize(keySpan);

                if (!db.IsValidPage(header.RootSubIndexNode.TargetPageID))
                    continue;

                var root = NodeManager.GetIndexNode(db, header.RootSubIndexNode.TargetPageID, header.RootSubIndexNode.TargetNodeIndex);
                if (root == null)
                    continue;

                var existing = IndexManager.GetDataByIndex(db, tablePage, root.Value, keySpan);
                if (existing != null && (!excludeDataId.HasValue || existing.Id != excludeDataId.Value))
                    throw LumException.Raise($"{LumExceptionMessage.KeyAlreadyExisted}: {header.Name.TransformToToString()}");
            }
        }


        public static DataNode? FirstOrDefaultNode(DbCache db, TablePage tablePage, string columnName, DbCell value)
        {
            var headerIndex = tablePage.GetTableHeaderIndex(columnName);
            var columnHeader = tablePage.ColumnHeaders[headerIndex];
            if (columnHeader.IsKey == false)
            {
                return null;
            }

            var cell = value.WithColumnType(columnHeader.ValueType);
            if (!columnHeader.ValueType.IsValidFix32())
            {
                return null;
            }

            var len = columnHeader.ValueType.GetLength();
            Span<byte> buffer = stackalloc byte[len];
            cell.Serialize(buffer);
            return IndexManager.GetDataByIndex(db, tablePage, NodeManager.GetIndexNode(db, columnHeader.RootSubIndexNode.TargetPageID, columnHeader.RootSubIndexNode.TargetNodeIndex).Value, buffer);
        }

        public static IDbValue Pick(DbCache db, TablePage tablePage, string keyName, DbCell keyValue)
        {
            var headerIndex = tablePage.GetTableHeaderIndex(keyName);
            var columnHeader = tablePage.ColumnHeaders[headerIndex];

            if (columnHeader.IsKey == false)
            {
                return new DbValue(LumException.Raise($"{keyName} {LumExceptionMessage.NotKey}"));
            }

            if (!columnHeader.ValueType.IsValidFix32())
            {
                return new DbValue(LumException.Raise($"{LumExceptionMessage.DataTypeNotSupport}: {columnHeader.ValueType}"));
            }

            var node = FirstOrDefaultNode(db, tablePage, keyName, keyValue);
            if (node == null)
            {
                return new DbValue(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, {keyName}: {keyValue.ToObject()}"));
            }

            return new DbValue(DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, node.Data));
        }

        public static IDbValue Delete(DbCache db, TablePage tablePage, string keyName, DbCell keyValue)
        {
            var dataNode = FirstOrDefaultNode(db, tablePage, keyName, keyValue);
            return Delete(db, tablePage, dataNode);
        }

        public static DataNode? FirstOrDefaultNode(DbCache db, TablePage tablePage, uint id)
        {
            Span<byte> key = stackalloc byte[4];
            DbValueTypeUtils.WriteUInt32(id, key);
            return IndexManager.GetDataByIndex(db, tablePage, NodeManager.GetIndexNode(db, tablePage.PageHeader.RootIndexNode.TargetPageID, tablePage.PageHeader.RootIndexNode.TargetNodeIndex).Value, key);
        }

        public static IDbValue Pick(DbCache db, TablePage tablePage, uint id)
        {
            Span<byte> key = stackalloc byte[4];
            DbValueTypeUtils.WriteUInt32(id, key);
            var node = FirstOrDefaultNode(db, tablePage, id);

            if (node == null)
            {
                return new DbValue(LumException.Raise($"{LumExceptionMessage.KeyNoFound}, id: {id}"));
            }
            else
            {
                return new DbValue(DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, node.Data));
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
                var dbResult = new DbValue(DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, dataNode.Data));
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

        internal static void Update(DbCache db, TablePage tablePage, DataNode dataNode, string columnName, DbCell value)
        {
            var headerIndex = tablePage.GetTableHeaderIndex(columnName);
            var header = tablePage.ColumnHeaders[headerIndex];

            var valueSpan = dataNode.Data.Slice(DataManager.GetDataOffset(tablePage.ColumnHeaders, headerIndex), header.ValueType.GetLength());
            var originCell = valueSpan.DeserializeBytesToCell(db, header.ValueType);
            var newCell = value.WithColumnType(header.ValueType);
            newCell.EnsureFitsColumn(header.ValueType);

            if (header.IsKey && !CellsEqual(originCell, newCell, header.ValueType))
            {
                var staged = new TableValue[tablePage.ColumnHeaders.Length];
                for (int i = 0; i < tablePage.ColumnHeaders.Length; i++)
                {
                    var h = tablePage.ColumnHeaders[i];
                    if (i == headerIndex)
                        staged[i] = (h.Name.TransformToToString(), newCell);
                    else
                    {
                        var span = dataNode.Data.Slice(DataManager.GetDataOffset(tablePage.ColumnHeaders, i), h.ValueType.GetLength());
                        staged[i] = (h.Name.TransformToToString(), span.DeserializeBytesToCell(db, h.ValueType));
                    }
                }
                EnsureNoDuplicateBusinessKeys(db, tablePage, staged, dataNode.Id);
            }

            if (!CellsEqual(originCell, newCell, header.ValueType))
            {
                var oldKey = valueSpan.ToArray();
                DataManager.UpdateSingleData(db, header, dataNode, newCell, headerIndex);
                if (header.IsKey)
                {
                    IndexManager.UpdateIndex(db, tablePage, dataNode, header, header.Name, oldKey);
                }
            }
        }

        internal static void Update(DbCache db, TablePage tablePage, DataNode dataNode, DbCell[] cells)
        {
            // Pre-check key uniqueness for changed key columns (edit allowed; collision with another row not).
            var staged = new TableValue[tablePage.ColumnHeaders.Length];
            for (int i = 0; i < tablePage.ColumnHeaders.Length; i++)
            {
                var header = tablePage.ColumnHeaders[i];
                var newCell = cells[i].WithColumnType(header.ValueType);
                newCell.EnsureFitsColumn(header.ValueType);
                staged[i] = (header.Name.TransformToToString(), newCell);
            }
            EnsureNoDuplicateBusinessKeys(db, tablePage, staged, dataNode.Id);

            for (int i = 0; i < tablePage.ColumnHeaders.Length; i++)
            {
                var header = tablePage.ColumnHeaders[i];
                var valueSpan = dataNode.Data.Slice(DataManager.GetDataOffset(tablePage.ColumnHeaders, i), header.ValueType.GetLength());
                var originCell = valueSpan.DeserializeBytesToCell(db, header.ValueType);
                var newCell = cells[i].WithColumnType(header.ValueType);

                if (!CellsEqual(originCell, newCell, header.ValueType))
                {
                    var oldKey = valueSpan.ToArray();
                    DataManager.UpdateData(db, tablePage, dataNode, cells, i);
                    if (header.IsKey)
                    {
                        IndexManager.UpdateIndex(db, tablePage, dataNode, header, header.Name, oldKey);
                    }
                }
            }
        }

        private static bool CellsEqual(in DbCell a, in DbCell b, DbValueType type)
        {
            var ca = a.WithColumnType(type);
            var cb = b.WithColumnType(type);
            return type switch
            {
                DbValueType.Bool => ca.AsBool() == cb.AsBool(),
                DbValueType.Byte => ca.AsByte() == cb.AsByte(),
                DbValueType.Int => ca.AsInt() == cb.AsInt(),
                DbValueType.UInt => ca.AsUInt() == cb.AsUInt(),
                DbValueType.Long => ca.AsLong() == cb.AsLong(),
                DbValueType.ULong => ca.AsULong() == cb.AsULong(),
                DbValueType.Float => ca.AsFloat().Equals(cb.AsFloat()),
                DbValueType.Double => ca.AsDouble().Equals(cb.AsDouble()),
                DbValueType.Decimal => ca.AsDecimal() == cb.AsDecimal(),
                DbValueType.DateTimeUTC => ca.AsDateTimeUtc() == cb.AsDateTimeUtc(),
                DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B or DbValueType.StrVar
                    => ca.AsString() == cb.AsString(),
                DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar
                    => ca.AsBytes().AsSpan().SequenceEqual(cb.AsBytes()),
                _ => false,
            };
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

        public static void GoThrough(DbCache db, TablePage tablePage, RowViewAction action)
        {
            if (db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
                DataManager.GoThrough(db, tablePage.ColumnHeaders, rootPage!, action);
            }
        }

        public static void GoThrough(DbCache db, TablePage tablePage, RowViewIdAction action)
        {
            if (db.IsValidPage(tablePage.PageHeader.RootDataPageId))
            {
                var rootPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
                DataManager.GoThrough(db, tablePage.ColumnHeaders, rootPage!, action);
            }
        }
    }
}