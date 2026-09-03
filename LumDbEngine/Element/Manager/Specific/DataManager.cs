using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Extension.DbEntity;
using LumDbEngine.Utils.ByteUtils;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace LumDbEngine.Element.Manager.Specific
{
    internal partial class DataManager
    {
        public static void InitializeDataPage(TablePage tablePage, DataPage dataPage)
        {
            tablePage.SetAvailableDataPageId(dataPage.PageId);

            tablePage.SetLastDataPageId(dataPage.PageId);

            dataPage.MaxDataCount = (int)(DataPage.MAX_TOTAL_DATA_SIZE / ((double)tablePage.PageHeader.DataLength));

            Debug.Assert(dataPage.MaxDataCount != 0);

            dataPage.ResetDataNodesSize();

            dataPage.DataLenthPerNode = tablePage.ColumnHeaders.Sum(o => o.ValueType.GetLength());

            for (byte i = 0; i < dataPage.MaxDataCount; i++)
            {
                dataPage.DataNodes[i] = new DataNode(dataPage.PageId, dataPage.DataLenthPerNode) { NodeIndex = i, NextFreeNodeIndex = (byte)(i + 1) };
            }

            dataPage.MarkDirty();
        }

        public static DataPage InitializeNewDataPage(DbCache db, TablePage tablePage)
        {
            var dataPage = PageManager.RequestAvailablePage<DataPage>(db);
            DataManager.InitializeDataPage(tablePage, dataPage);
            tablePage.SetRootDataPageId(dataPage.PageId);
            return dataPage;
        }

        public static DataPage RequestAvailableDataPage(DbCache db, TablePage tablePage)
        {
            DataPage dataPage;

            if (db.IsValidPage(tablePage.PageHeader.AvailableDataPage))
            {
                dataPage = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.AvailableDataPage);
            }
            else
            {
                dataPage = InitializeNewDataPage(db, tablePage);
            }

            LumException.ThrowIfNull(dataPage, "Page data error");

            if (dataPage!.AvailableNodeIndex < dataPage.MaxDataCount)
            {
                return dataPage;
            }
            else
            {
                var newDataPage = PageManager.RequestAvailablePage<DataPage>(db);

                InitializeDataPage(tablePage, newDataPage);

                PageManager.LinkPage(dataPage, newDataPage);
                return newDataPage;
            }
        }

        public static DataNode InsertValueToDataPage(DbCache db, TablePage tablePage, DataPage dataPage, TableValue[] valuesOrdered)
        {
            var node = SetValueToDataPage(db, tablePage, dataPage, valuesOrdered);
            return node;
        }

        private static unsafe void CopyToSpan(Span<byte> bytes, Span<byte> span, int length, ref int offset)
        {
            if (bytes.Length > length)
            {
                throw LumException.Raise("Unknown data error.");
            }
            else if (bytes.Length < length)
            {
                Span<byte> paddingBuffer = stackalloc byte[length];

                bytes.PaddingToBytes(paddingBuffer, length);

                paddingBuffer.CopyTo(span.Slice(offset, length));
            }
            else
            {
                bytes.CopyTo(span.Slice(offset, length));
            }

            offset += length;
        }

        public static unsafe DataNode SetValueToDataPage(DbCache db, TablePage tablePage, DataPage dataPage, TableValue[] valuesOrdered)
        {           
            var node = dataPage.DataNodes[dataPage.AvailableNodeIndex]; //get the available node to store new data.

            Span<byte> dataSpan = stackalloc byte[dataPage.DataLenthPerNode];

            int offset = 0;

            Span<byte> bts = stackalloc byte[NodeLink.Size];

            for (int i = 0; i < valuesOrdered.Length; i++)
            {
                var tableValue = valuesOrdered[i];
                var headerType = tablePage.ColumnHeaders[i].ValueType;
                var cell = tableValue.value.WithColumnType(headerType);

                if (!headerType.CheckType(in cell))
                {
                    LumException.Throw($"Wrong type inserted to column {tableValue.columnName}");
                }

#pragma warning disable CA2014

                switch (headerType)
                {
                    case DbValueType.Decimal:
                        {
                            Span<byte> buffer = stackalloc byte[16];
                            cell.Serialize(buffer);
                            CopyToSpan(buffer, dataSpan, 16, ref offset);
                            break;
                        }
                    case DbValueType.Bool:
                    case DbValueType.Byte:
                        {
                            Span<byte> buffer = stackalloc byte[1];
                            cell.Serialize(buffer);
                            CopyToSpan(buffer, dataSpan, 1, ref offset);
                            break;
                        }
                    case DbValueType.Int:
                    case DbValueType.UInt:
                    case DbValueType.Float:
                        {
                            Span<byte> buffer = stackalloc byte[4];
                            cell.Serialize(buffer);
                            CopyToSpan(buffer, dataSpan, 4, ref offset);
                            break;
                        }
                    case DbValueType.Str8B:
                    case DbValueType.Bytes8:
                    case DbValueType.Long:
                    case DbValueType.ULong:
                    case DbValueType.Double:
                    case DbValueType.DateTimeUTC:
                        {
                            Span<byte> buffer = stackalloc byte[8];
                            cell.Serialize(buffer);
                            CopyToSpan(buffer, dataSpan, 8, ref offset);
                        }
                        break;

                    case DbValueType.Str16B:
                    case DbValueType.Bytes16:
                        {
                            Span<byte> buffer = stackalloc byte[16];
                            cell.Serialize(buffer);
                            CopyToSpan(buffer, dataSpan, 16, ref offset);
                            break;
                        }

                    case DbValueType.Str32B:
                    case DbValueType.Bytes32:
                        {
                            Span<byte> buffer = stackalloc byte[32];
                            cell.Serialize(buffer);
                            CopyToSpan(buffer, dataSpan, 32, ref offset);
                            break;
                        }

                    case DbValueType.StrVar:
                        {
                            var link = DataVarManager.InsertDataVar(db, GetVarPayload(cell, DbValueType.StrVar));
                            var linkBytes = (new NodeLink() { TargetPageID = link.pageId, TargetNodeIndex = link.nodeIndex }).ToBytesAndSpan(bts);
                            CopyToSpan(linkBytes, dataSpan, NodeLink.Size, ref offset);
                            break;
                        }
                    case DbValueType.BytesVar:
                        {
                            try
                            {
                                var link = DataVarManager.InsertDataVar(db, cell.AsBytes().AsSpan());
                                var linkBytes = (new NodeLink() { TargetPageID = link.pageId, TargetNodeIndex = link.nodeIndex }).ToBytesAndSpan(bts);
                                CopyToSpan(linkBytes, dataSpan, NodeLink.Size, ref offset);
                                break;
                            }
                            catch (Exception ex)
                            {
                                throw LumException.Raise($"Type error: {ex.Message}");
                            }
                        }
                    default:
                        throw LumException.Raise("Unknown value type");
                }
            }

            dataSpan.CopyTo(node.Data);
            node.IsAvailable = true;
            node.Id = tablePage.GetNextDataIdAndAutoIncrement();    // auto increment

            dataPage.AvailableNodeIndex = node.NextFreeNodeIndex;
            dataPage.MarkDirty();

            return node;
        }

        internal static DbCell[] GetCells(DbCache db, ColumnHeader[] headers, Span<byte> value)
        {
            var cells = new DbCell[headers.Length];

            var dataOffset = 0;

            for (int i = 0; i < headers.Length; i++)
            {
                var valueTypeLength = headers[i].ValueType.GetLength();
                cells[i] = value.Slice(dataOffset, valueTypeLength).DeserializeBytesToCell(db, headers[i].ValueType);
                dataOffset += valueTypeLength;
            }

            return cells;
        }

        /// <summary>
        /// Copy row bytes once; fixed columns decode on demand; var columns resolved immediately.
        /// </summary>
        internal static DbRowBuffer CreateRowBuffer(DbCache db, ColumnHeader[] headers, Span<byte> value)
        {
            var types = new DbValueType[headers.Length];
            var offsets = new int[headers.Length];
            var varPayload = new object?[headers.Length];

            var dataOffset = 0;
            for (int i = 0; i < headers.Length; i++)
            {
                var type = headers[i].ValueType;
                var len = type.GetLength();
                types[i] = type;
                offsets[i] = dataOffset;

                if (type == DbValueType.StrVar || type == DbValueType.BytesVar)
                {
                    // Resolve var columns from the live page span before copying the row.
                    var cell = value.Slice(dataOffset, len).DeserializeBytesToCell(db, type);
                    varPayload[i] = type == DbValueType.StrVar ? cell.AsString() : cell.AsBytes();
                }

                dataOffset += len;
            }

            return new DbRowBuffer(value.ToArray(), types, offsets, varPayload);
        }

        internal static void DeleteDataNodeByIndex(DbCache db, TablePage tablePage, DataNode dataNode)
        {
            // mark the dataNode as freeNode
            var dataPage = PageManager.GetPage<DataPage>(db, dataNode.HostPageId);
            dataPage.MarkDirty();

            dataNode.IsAvailable = false;
            dataNode.NextFreeNodeIndex = dataPage.AvailableNodeIndex;

            dataPage.AvailableNodeIndex = dataNode.NodeIndex;
            dataPage.CurrentDataCount--;
            dataPage.MarkDirty();

            if (dataPage.CurrentDataCount == 0)
            {
                if (tablePage.PageHeader.AvailableDataPage == dataPage.PageId)
                {
                    tablePage.SetAvailableDataPageId(uint.MaxValue);
                }

                if (tablePage.PageHeader.RootDataPageId == dataPage.PageId)
                {
                    tablePage.SetRootDataPageId(dataPage.NextPageId);
                }

                PageManager.RecyclePage(db, dataPage);
            }

            // clean var data node
            int dataOffset = 0;

            for (int i = 0; i < tablePage.ColumnHeaders.Length; i++)
            {
                var hd = tablePage.ColumnHeaders[i];

                var valueTypeLength = hd.ValueType.GetLength();

                if ((byte)hd.ValueType > DbValueTypeUtils.DataVarSplitter)
                {
                    NodeLink.Create(dataNode.Data.Slice(dataOffset), out var link);
                    DataVarManager.DeleteDataVarNode(db, link);
                }
                dataOffset += valueTypeLength;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateData(DbCache db, TablePage tablePage, DataNode dataNode, DbCell[] cells, int index)
        {
            UpdateSingleData(db, tablePage.ColumnHeaders[index], dataNode, cells[index], index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateSingleData(DbCache db, ColumnHeader header, DataNode dataNode, DbCell value, int index)
        {
            db.MarkDirtyAndCachePage(db, dataNode.HostPageId);
            var cell = value.WithColumnType(header.ValueType);
            LumException.ThrowIfNotTrue(header.ValueType.CheckType(in cell), "data type error");

            if ((byte)header.ValueType < DbValueTypeUtils.DataVarSplitter)
            {
                var len = header.ValueType.GetLength();
                Span<byte> buffer = stackalloc byte[len];
                cell.Serialize(buffer);
                int dataOffset = GetDataOffset(header.Page.ColumnHeaders, index);

                if (header.ValueType is DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B
                    or DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32)
                {
                    Span<byte> paddingBuffer = stackalloc byte[len];
                    buffer.PaddingToBytes(paddingBuffer, len);
                    paddingBuffer.CopyTo(dataNode.Data.Slice(dataOffset, len));
                }
                else
                {
                    buffer.CopyTo(dataNode.Data.Slice(dataOffset, len));
                }
            }
            else
            {
                int dataOffset = GetDataOffset(header.Page.ColumnHeaders, index);
                NodeLink.Create(dataNode.Data.Slice(dataOffset, header.ValueType.GetLength()), out var link);

                DataVarManager.UpdateData(db, ref link, GetVarPayload(cell, header.ValueType));

                Span<byte> bts = stackalloc byte[NodeLink.Size];
                var linkBytes = link.ToBytesAndSpan(bts);
                linkBytes.CopyTo(dataNode.Data.Slice(dataOffset, linkBytes.Length));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetVarPayload(in DbCell cell, DbValueType type)
        {
            return type == DbValueType.StrVar
                ? Encoding.UTF8.GetBytes(cell.AsString())
                : cell.AsBytes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetDataOffset(ColumnHeader[] headers, int index)
        {
            int offset = 0;
            for (int i = 0; i < index; i++)
            {
                offset += headers[i].ValueType.GetLength();
            }
            return offset;
        }

        internal static void GetDataPagesAndRemoveDataVar(DbCache db, TablePage tablePage, DataPage dataPage, HashSet<uint> pages)
        {
            var varIndexList = tablePage.ColumnHeaders.Select((o, i) => (i, o)).Where(o => (byte)o.o.ValueType > DbValueTypeUtils.DataVarSplitter).Select(o => o.i).ToArray();
            var dataOffsetList = varIndexList.Select(o => GetDataOffset(tablePage.ColumnHeaders, o)).ToArray();

            var dataLengthList = varIndexList.Select(o => tablePage.ColumnHeaders[o].ValueType.GetLength()).ToArray();

            while (dataPage != null)
            {
                pages.Add(dataPage.PageId);

                for (int di = 0; di < dataPage.MaxDataCount; di++)
                {
                    var data = dataPage.DataNodes[di];

                    if (data.IsAvailable)
                    {
                        for (int i = 0; i < varIndexList.Length; i++)
                        {
                            var index = varIndexList[i];
                            NodeLink.Create(data.Data.Slice(dataOffsetList[i], dataLengthList[i]), out var nodeLink);
                            DataVarManager.DeleteDataVarNode(db, nodeLink);
                        }
                    }
                }

                if (db.IsValidPage(dataPage.NextPageId))
                {
                    dataPage = PageManager.GetPage<DataPage>(db, dataPage.NextPageId);
                    dataPage.MarkDirty();
                }
                else
                {
                    dataPage = null;
                }
            }
        }

        internal static void GoThrough(DbCache db, ColumnHeader[] headers, DataPage? page, RowViewAction action)
        {
            GoThrough(db, headers, page, (uint id, ref RowView view) => action(ref view), backward: false);
        }

        internal static void FillColumnOffsets(ColumnHeader[] headers, Span<int> offsets)
        {
            var pos = 0;
            for (int c = 0; c < headers.Length; c++)
            {
                offsets[c] = pos;
                pos += headers[c].ValueType.GetLength();
            }
        }

        internal static void GoThrough(DbCache db, ColumnHeader[] headers, DataPage? page, RowViewIdAction action)
        {
            GoThrough(db, headers, page, action, backward: false);
        }

        /// <summary>
        /// Occupancy-order scan. Forward: Root → Next. Backward walks Next to the chain
        /// end then Prev (does not use LastDataPageId). Page linking is unchanged.
        /// </summary>
        internal static void GoThrough(DbCache db, ColumnHeader[] headers, DataPage? page, RowViewIdAction action, bool backward)
        {
            if (page == null)
                return;

            Span<int> offsets = stackalloc int[headers.Length];
            FillColumnOffsets(headers, offsets);

            if (backward)
            {
                while (db.IsValidPage(page.NextPageId))
                    page = PageManager.GetPage<DataPage>(db, page.NextPageId);

                while (page != null)
                {
                    for (int i = page.MaxDataCount - 1; i >= 0; i--)
                    {
                        var dataNode = page.DataNodes[i];
                        if (dataNode != null && dataNode.IsAvailable)
                        {
                            var view = new RowView(dataNode.Data, headers, offsets, db);
                            if (!action(dataNode.Id, ref view))
                                return;
                        }
                    }

                    page = db.IsValidPage(page.PrevPageId)
                        ? PageManager.GetPage<DataPage>(db, page.PrevPageId)
                        : null;
                }

                return;
            }

            while (page != null)
            {
                for (int i = 0; i < page.MaxDataCount; i++)
                {
                    var dataNode = page.DataNodes[i];

                    if (dataNode.IsAvailable)
                    {
                        var view = new RowView(dataNode.Data, headers, offsets, db);
                        if (!action(dataNode.Id, ref view))
                        {
                            return;
                        }
                    }
                }

                if (db.IsValidPage(page.NextPageId))
                {
                    page = PageManager.GetPage<DataPage>(db, page.NextPageId);
                }
                else
                {
                    page = null;
                }
            }
        }

    }
}