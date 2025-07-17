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

                var typeCheck = DbValueTypeUtils.CheckType(tablePage.ColumnHeaders[i].ValueType, tableValue.value);

                if (!typeCheck)
                {
                    LumException.Throw($"Wrong type inserted to column {tableValue.columnName}");
                }

#pragma warning disable CA2014

                switch (tablePage.ColumnHeaders[i].ValueType)
                {
                    case DbValueType.Decimal:
                        {
                            Span<byte> buffer = stackalloc byte[16];
                            tableValue.value.SerializeObjectToBytes(buffer);
                            CopyToSpan(buffer, dataSpan, 16, ref offset);
                            break;
                        }
                    case DbValueType.Bool:
                    case DbValueType.Byte:
                        {
                            Span<byte> buffer = stackalloc byte[1];
                            tableValue.value.SerializeObjectToBytes(buffer);
                            CopyToSpan(buffer, dataSpan, 1, ref offset);
                            break;
                        }
                    case DbValueType.Int:
                    case DbValueType.UInt:
                    case DbValueType.Float:
                        {
                            Span<byte> buffer = stackalloc byte[4];
                            tableValue.value.SerializeObjectToBytes(buffer);
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
                            tableValue.value.SerializeObjectToBytes(buffer);
                            CopyToSpan(buffer, dataSpan, 8, ref offset);
                        }
                        break;

                    case DbValueType.Str16B:
                    case DbValueType.Bytes16:
                        {
                            Span<byte> buffer = stackalloc byte[16];
                            tableValue.value.SerializeObjectToBytes(buffer);
                            CopyToSpan(buffer, dataSpan, 16, ref offset);
                            break;
                        }

                    case DbValueType.Str32B:
                    case DbValueType.Bytes32:
                        {
                            Span<byte> buffer = stackalloc byte[32];
                            tableValue.value.SerializeObjectToBytes(buffer);
                            CopyToSpan(buffer, dataSpan, 32, ref offset);
                            break;
                        }

                    case DbValueType.StrVar:
                        {
                            var len = System.Text.Encoding.UTF8.GetByteCount((string)tableValue.value);
                            Span<byte> buffer = stackalloc byte[len];
                            tableValue.value.SerializeObjectToBytes(buffer);
                            var link = DataVarManager.InsertDataVar(db, buffer);
                            var linkBytes = (new NodeLink() { TargetPageID = link.pageId, TargetNodeIndex = link.nodeIndex }).ToBytesAndSpan(bts);
                            CopyToSpan(linkBytes, dataSpan, NodeLink.Size, ref offset);
                            break;
                        }
                    case DbValueType.BytesVar:
                        {
                            try
                            {
                                var link = DataVarManager.InsertDataVar(db, ((byte[])tableValue.value).AsSpan());
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

        internal static IEnumerable<(DataNode node, object[] data)> GetValues(DbCache db, ColumnHeader[] headers, DataPage? page)
        {
            while (page != null)
            {
                for (int i = 0; i < page.MaxDataCount; i++)
                {
                    var dataNode = page.DataNodes[i];

                    if (dataNode.IsAvailable)
                    {
                        yield return (dataNode, GetValue(db, headers, dataNode.Data));
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
        
        internal static IEnumerable<(DataNode node, object[] data)> GetValues_Backward(DbCache db, ColumnHeader[] headers, DataPage? page)
        {
            uint initPageId = page?.PageId ?? uint.MaxValue;
            uint nextId = page?.NextPageId ?? uint.MaxValue;

            while (db.IsValidPage(nextId))
            {
                initPageId = nextId;
                nextId = GetNextPageId(db, PageType.Data, initPageId);
            }

            page = PageManager.GetPage<DataPage>(db, initPageId);

            while (page != null)
            {
                for (int i = page.MaxDataCount - 1; i >= 0; i--)
                {
                    var dataNode = page.DataNodes[i];

                    if (dataNode.IsAvailable)
                    {
                        yield return (dataNode, GetValue(db, headers, dataNode.Data));
                    }
                }

                if (db.IsValidPage(page.PrevPageId))
                {
                    page = PageManager.GetPage<DataPage>(db, page.PrevPageId);
                }
                else
                {
                    page = null;
                }
            }
        }

      
        private static uint GetNextPageId(DbCache db, PageType pageType, uint pageId)
        {
            using var reader = db.iof.RentReader();
            return BasePage.ReadPageInfo(pageId,pageType, reader).nextPageId;
        }

        internal static object[] GetValue(DbCache db, ColumnHeader[] headers, Span<byte> value)
        {
            var objects = new object[headers.Length];

            var dataOffset = 0;

            for (int i = 0; i < headers.Length; i++)
            {
                var valueTypeLength = headers[i].ValueType.GetLength();
                objects[i] = value.Slice(dataOffset, valueTypeLength).DeserializeBytesToValue(db, headers[i].ValueType);
                dataOffset += valueTypeLength;
            }

            return objects;
        }

        private static bool CheckCondition(DbCache db, ColumnHeader[] fullColumnHeaders, Func<object,bool>? []? conditions, Span<byte> value)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return true;
            }

            var dataOffset = 0;

            for (int i = 0; i < fullColumnHeaders.Length; i++)
            {
                var valueTypeLength = fullColumnHeaders[i].ValueType.GetLength();

                if (conditions[i] != null)
                {
                    var obj = value.Slice(dataOffset, valueTypeLength).DeserializeBytesToValue(db, fullColumnHeaders[i].ValueType);
                    
                    if (conditions[i]!(obj) == false) // keep the value with a conditions result of true
                    {
                        return false;
                    }
                }

                dataOffset += valueTypeLength;
            }
            return true;
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
        internal static void UpdateData(DbCache db, TablePage tablePage, DataNode dataNode, object[] values, int index)
        {
            UpdateSingleData(db, tablePage.ColumnHeaders[index], dataNode, values[index], index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void UpdateSingleData(DbCache db, ColumnHeader header, DataNode dataNode, object value, int index)
        {
            db.MarkDirtyAndCachePage(db, dataNode.HostPageId);

            if ((byte)header.ValueType < DbValueTypeUtils.DataVarSplitter)
            {
                LumException.ThrowIfNotTrue(DbValueTypeUtils.CheckType(header.ValueType, value), "data type error");

                Span<byte> buffer = stackalloc byte[header.ValueType.GetLength()];
                value.SerializeObjectToBytes(buffer);

                var len = header.ValueType.GetLength();

                if (value is string)
                {
                    Span<byte> paddingBuffer = stackalloc byte[len];

                    buffer.PaddingToBytes(paddingBuffer, len);
                    int dataOffset = GetDataOffset(header.Page.ColumnHeaders, index);
                    paddingBuffer.CopyTo(dataNode.Data.Slice(dataOffset, len));
                }
                else
                {
                    int dataOffset = GetDataOffset(header.Page.ColumnHeaders, index);
                    buffer.CopyTo(dataNode.Data.Slice(dataOffset, len));
                }
            }
            else
            {
                int dataOffset = GetDataOffset(header.Page.ColumnHeaders, index);
                NodeLink.Create(dataNode.Data.Slice(dataOffset, header.Page.ColumnHeaders[index].ValueType.GetLength()), out var link);

                int len;
                if (header.ValueType == DbValueType.StrVar)
                {
                    len = System.Text.Encoding.UTF8.GetByteCount((string)value);
                }
                else
                {
                    len = ((byte[])value).Length;
                }

                Span<byte> paddingBuffer = stackalloc byte[len];

                value.SerializeObjectToBytes(paddingBuffer);
                DataVarManager.UpdateData(db, ref link, paddingBuffer);

                unsafe
                {
                    Span<byte> bts = stackalloc byte[NodeLink.Size];
                    var linkBytes = link.ToBytesAndSpan(bts);
                    linkBytes.CopyTo(dataNode.Data.Slice(dataOffset, linkBytes.Length));
                }
            }
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

        internal static uint CountWithCnditions(DbCache db, ColumnHeader[] columnHeaders, Func<object, bool>?[] conditions, DataPage? page)
        {
            uint sum = 0;

            while (page != null)
            {
                for (int i = 0; i < page.MaxDataCount; i++)
                {
                    var dataNode = page.DataNodes[i];

                    if (dataNode.IsAvailable && CheckCondition(db, columnHeaders, conditions, dataNode.Data))
                    {
                        sum++;
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

            return sum;
        }

        internal static IEnumerable<(DataNode node, object[] data)> GetValuesWithIdCondition(DbCache db, ColumnHeader[] headers, DataPage? page, Func<object, bool>?[]? conditions,uint skip,uint limit)
        {
            int currentCount = 0;
            int currentSkip = 0;

            while (page != null)
            {
                for (int i = 0; i < page.MaxDataCount; i++)
                {
                    var dataNode = page.DataNodes[i];

                    if (dataNode.IsAvailable && (CheckCondition(db, headers, conditions, dataNode.Data)))
                    {
                        if (skip == 0 || currentSkip >= skip)
                        {
                            if (limit == 0 || currentCount < limit)
                            {
                                currentCount++;
                                yield return (dataNode, GetValue(db, headers, dataNode.Data));
                            }
                            else
                            {
                                goto end;
                            }
                        }
                        else
                        {
                            currentSkip++;
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

            end:;
        }

        internal static IEnumerable<(DataNode node, object[] data)> GetValuesWithIdCondition_Backward(DbCache db, ColumnHeader[] headers, DataPage? page, Func<object, bool>?[]? conditions, uint skip, uint limit)
        {
            uint initPageId = page?.PageId ?? uint.MaxValue;
            uint nextId = page?.NextPageId ?? uint.MaxValue;

            while (db.IsValidPage(nextId))
            {
                initPageId = nextId;
                nextId = GetNextPageId(db, PageType.Data, initPageId);
            }


            int currentCount = 0;
            int currentSkip = 0;

            page = PageManager.GetPage<DataPage>(db, initPageId);

            while (page != null)
            {
                for (int i = page.MaxDataCount - 1; i >= 0; i--)
                {
                    var dataNode = page.DataNodes[i];

                    if (dataNode.IsAvailable && (CheckCondition(db, headers, conditions, dataNode.Data)))
                    {
                        if (skip == 0 || currentSkip >= skip)
                        {
                            if (limit == 0 || currentCount < limit)
                            {
                                currentCount++;
                                yield return (dataNode, GetValue(db, headers, dataNode.Data));
                            }
                            else
                            {
                                goto end;
                            }
                        }
                        else
                        {
                            currentSkip++;
                        }
                    }
                }

                if (db.IsValidPage(page.PrevPageId))
                {
                    page = PageManager.GetPage<DataPage>(db, page.PrevPageId);
                }
                else
                {
                    page = null;
                }
            }
            end:;
        }

        
        internal static void GoThrough(DbCache db, ColumnHeader[] headers, DataPage? page, Func<object[], bool>  action)
        {
            while (page != null)
            {
                for (int i = 0; i < page.MaxDataCount; i++)
                {
                    var dataNode = page.DataNodes[i];

                    if (dataNode.IsAvailable)
                    {
                        if(!action(GetValue(db, headers, dataNode.Data)))
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