using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.KeyIndex;
using LumDbEngine.Element.Structure.Page.Repo;
using LumDbEngine.Utils.ByteUtils;
using LumDbEngine.Utils.StringUtils;
using System.Text;

namespace LumDbEngine.Element.Manager.Specific
{
    /// <summary>
    /// Rewrites one table's rows at a new column width inside the current cache.
    /// The table page id stays put. Old data and index pages are recycled only after
    /// the new chain is complete. Callers reset the cache if this throws after it has
    /// started mutating, so a failed alter cannot be committed.
    /// </summary>
    internal static class TableAlterManager
    {
        public static void AddColumn(DbCache db, string tableName, string columnName, DbValueType type, bool isKey, DbCell? defaultValue, int? ordinal)
        {
            var table = RequireTable(db, tableName);
            var oldHeaders = table.ColumnHeaders;
            int at = ordinal ?? oldHeaders.Length;

            Reject(string.IsNullOrWhiteSpace(columnName), "Column name is empty");
            Reject(Encoding.UTF8.GetByteCount(columnName) > ColumnHeader.NameLength, $"Column name is longer than {ColumnHeader.NameLength} bytes");
            Reject(table.IsHeaderExists(columnName), LumExceptionMessage.DuplicateColumnHeader);
            Reject(oldHeaders.Length + 1 > TablePage.MAX_COLUMN_COUNT, "Excessive number of columns");
            Reject(at < 0 || at > oldHeaders.Length, "Column ordinal is out of range");
            Reject(type == DbValueType.Unknow, LumExceptionMessage.UnknownValType);
            Reject(isKey && !type.IsValidFix32(), $"{LumExceptionMessage.DataTypeNotSupport}: {type}");

            if (defaultValue.HasValue)
                ValidateDefault(type, defaultValue.Value);

            int live = CountLive(db, table);
            Reject(isKey && live > 1, "Cannot add a key column with one default when the table has more than one row");

            var source = new int[oldHeaders.Length + 1];
            var neu = new ColumnHeader[oldHeaders.Length + 1];
            int oldIndex = 0;
            for (int i = 0; i < neu.Length; i++)
            {
                if (i == at)
                {
                    neu[i] = NewHeader(table, columnName, type, isKey);
                    source[i] = -1;
                }
                else
                {
                    neu[i] = CloneHeader(table, oldHeaders[oldIndex]);
                    source[i] = oldIndex;
                    oldIndex++;
                }
            }

            Rewrite(db, table, oldHeaders, neu, source, defaultValue);
        }

        public static void DropColumn(DbCache db, string tableName, string columnName)
        {
            var table = RequireTable(db, tableName);
            var oldHeaders = table.ColumnHeaders;
            Reject(string.IsNullOrWhiteSpace(columnName), "Column name is empty");
            Reject(!table.IsHeaderExists(columnName), $"{LumExceptionMessage.ColumnNameNotExisted}:{columnName}");
            Reject(oldHeaders.Length <= 1, "One column at least");

            int drop = table.GetTableHeaderIndex(columnName);
            var neu = new ColumnHeader[oldHeaders.Length - 1];
            var source = new int[neu.Length];
            int n = 0;
            for (int i = 0; i < oldHeaders.Length; i++)
            {
                if (i == drop)
                    continue;
                neu[n] = CloneHeader(table, oldHeaders[i]);
                source[n] = i;
                n++;
            }

            Rewrite(db, table, oldHeaders, neu, source, defaultValue: null);
        }

        private static void Rewrite(DbCache db, TablePage table, ColumnHeader[] oldHeaders, ColumnHeader[] newHeaders, int[] sourceIndex, DbCell? defaultValue)
        {
            uint dataLength = 0;
            for (int i = 0; i < newHeaders.Length; i++)
                dataLength += (uint)newHeaders[i].ValueType.GetLength() + DataNode.HEADER_SIZE;

            int slots = (int)(DataPage.MAX_TOTAL_DATA_SIZE / (double)dataLength);
            Reject(slots < 1 || slots > byte.MaxValue, "The row does not fit in a data page");

            var oldOffsets = Offsets(oldHeaders);
            var newWidth = newHeaders.Sum(h => h.ValueType.GetLength());
            var kept = new bool[oldHeaders.Length];
            for (int i = 0; i < sourceIndex.Length; i++)
            {
                if (sourceIndex[i] >= 0)
                    kept[sourceIndex[i]] = true;
            }

            var oldRootData = table.PageHeader.RootDataPageId;
            var oldIndexPages = CollectIndexPages(db, table);

            var rows = new List<(uint id, byte[] data)>();
            var droppedVars = new List<NodeLink>();
            var oldDataPages = new List<uint>();
            if (db.IsValidPage(oldRootData))
            {
                uint pageId = oldRootData;
                while (db.IsValidPage(pageId) && !oldDataPages.Contains(pageId))
                {
                    oldDataPages.Add(pageId);
                    var page = PageManager.GetPage<DataPage>(db, pageId);
                    for (int i = 0; i < page.MaxDataCount; i++)
                    {
                        var node = page.DataNodes[i];
                        if (!node.IsAvailable)
                            continue;

                        var raw = node.Data.ToArray();
                        for (int c = 0; c < oldHeaders.Length; c++)
                        {
                            if (kept[c] || (byte)oldHeaders[c].ValueType <= DbValueTypeUtils.DataVarSplitter)
                                continue;
                            NodeLink.Create(raw.AsSpan(oldOffsets[c], NodeLink.Size), out var link);
                            if (db.IsValidPage(link.TargetPageID))
                                droppedVars.Add(link);
                        }

                        rows.Add((node.Id, raw));
                    }

                    pageId = page.NextPageId;
                }
            }

            uint nextDataId = table.PageHeader.NextDataId;

            table.SetColumnHeaders(newHeaders);
            table.SetColumnCount((byte)newHeaders.Length);
            table.SetDataLength(dataLength);
            table.PageHeader.RootIndexNode.Reset();
            table.PageHeader.AvailableIndexPage = uint.MaxValue;
            table.PageHeader.AvailableRepoPage = uint.MaxValue;
            table.SetRootDataPageId(uint.MaxValue);
            table.SetLastDataPageId(uint.MaxValue);
            table.SetAvailableDataPageId(uint.MaxValue);
            table.MarkDirty();

            CreateIndices(db, table, newHeaders);

            foreach (var (id, raw) in rows)
            {
                var built = new byte[newWidth];
                int dst = 0;
                for (int i = 0; i < newHeaders.Length; i++)
                {
                    int len = newHeaders[i].ValueType.GetLength();
                    if (sourceIndex[i] < 0)
                    {
                        WriteDefault(db, newHeaders[i].ValueType, defaultValue, built.AsSpan(dst, len));
                    }
                    else
                    {
                        int src = sourceIndex[i];
                        raw.AsSpan(oldOffsets[src], len).CopyTo(built.AsSpan(dst, len));
                    }

                    dst += len;
                }

                var node = DataManager.InsertPreservedRow(db, table, built, id);
                IndexManager.InsertMainIndex(db, table, node);
                for (int i = 0; i < newHeaders.Length; i++)
                {
                    if (!newHeaders[i].IsKey)
                        continue;
                    IndexManager.InsertSubIndex(db, table, node, newHeaders[i].Name.TransformToToString());
                }
            }

            foreach (var link in droppedVars)
                DataVarManager.DeleteDataVarNode(db, link);

            // New rows and their indexes already point at fresh pages. Retire the previous
            // data pages and every index/repo page (main index and each key sub-index),
            // then wipe payloads so a freed page does not keep the dropped column.
            var live = LivePages(db, table);
            ReleaseRetired(db, oldDataPages, live, table.PageId);
            ReleaseRetired(db, oldIndexPages, live, table.PageId);

            table.PageHeader.NextDataId = nextDataId;
            table.MarkDirty();
        }

        private static unsafe void CreateIndices(DbCache db, TablePage table, ColumnHeader[] headers)
        {
            var infos = new TableHeaderInfo[headers.Length];
            byte* mem = stackalloc byte[ColumnHeader.NameLength * headers.Length];
            for (int i = 0; i < headers.Length; i++)
            {
                var span = new Span<byte>(mem + i * ColumnHeader.NameLength, ColumnHeader.NameLength);
                headers[i].Name.CopyTo(span);
                infos[i].keyName = mem + i * ColumnHeader.NameLength;
                infos[i].type = headers[i].ValueType;
                infos[i].isKey = headers[i].IsKey;
            }

            IndexManager.CreateIndices(db, table, infos);
        }

        private static HashSet<uint> LivePages(DbCache db, TablePage table)
        {
            var pages = CollectIndexPages(db, table);
            if (!db.IsValidPage(table.PageHeader.RootDataPageId))
                return pages;

            uint pageId = table.PageHeader.RootDataPageId;
            while (db.IsValidPage(pageId) && pages.Add(pageId))
            {
                var page = PageManager.GetPage<DataPage>(db, pageId);
                pageId = page.NextPageId;
            }

            return pages;
        }

        /// <summary>
        /// Recycle pages that the rewritten table no longer references, and erase column
        /// values, index keys, and repo keys sitting on them.
        /// </summary>
        private static void ReleaseRetired(DbCache db, IEnumerable<uint> pages, HashSet<uint> live, uint tablePageId)
        {
            foreach (var pageId in pages)
            {
                if (pageId == tablePageId || live.Contains(pageId))
                    continue;

                var page = db[pageId];
                if (page == null || page.IsDeleted)
                    continue;

                PageManager.RecyclePage(db, page);
                ScrubRetired(page);
                page.MarkDirty();
            }
        }

        private static void ScrubRetired(BasePage page)
        {
            switch (page)
            {
                case IndexPage index:
                    for (int i = 0; i < index.Nodes.Length; i++)
                    {
                        ref var node = ref index.Nodes[i];
                        node.Id = 0;
                        node.Left.Reset();
                        node.Right.Reset();
                        node.KeyLink.Reset();
                        node.NodeIndex = (byte)i;
                    }
                    break;
                case RepoPage repo:
                    for (int i = 0; i < repo.Nodes.Length; i++)
                    {
                        ref var node = ref repo.Nodes[i];
                        var index = node.NodeIndex;
                        node.Reset(repo.PageId);
                        node.NodeIndex = index;
                    }
                    break;
                case DataPage data:
                    data.CurrentDataCount = 0;
                    if (data.DataNodes == null)
                        break;
                    for (int i = 0; i < data.DataNodes.Length; i++)
                    {
                        data.DataNodes[i].Data.Clear();
                        data.DataNodes[i].IsAvailable = false;
                        data.DataNodes[i].Id = 0;
                    }
                    break;
            }
        }

        private static HashSet<uint> CollectIndexPages(DbCache db, TablePage table)
        {
            var pages = new HashSet<uint>();
            var main = NodeManager.GetIndexNode(db, table.PageHeader.RootIndexNode.TargetPageID, table.PageHeader.RootIndexNode.TargetNodeIndex);
            IndexManager.GetIndexPages(db, main, pages);

            foreach (var header in table.ColumnHeaders)
            {
                if (!header.IsKey)
                    continue;
                var sub = NodeManager.GetIndexNode(db, header.RootSubIndexNode.TargetPageID, header.RootSubIndexNode.TargetNodeIndex);
                IndexManager.GetIndexPages(db, sub, pages);
            }

            uint repo = table.PageHeader.AvailableRepoPage;
            while (db.IsValidPage(repo) && pages.Add(repo))
            {
                var page = PageManager.GetPage<RepoPage>(db, repo);
                repo = page.NextPageId;
            }

            if (db.IsValidPage(table.PageHeader.AvailableIndexPage))
                pages.Add(table.PageHeader.AvailableIndexPage);

            pages.Remove(table.PageId);
            return pages;
        }

        private static void WriteDefault(DbCache db, DbValueType type, DbCell? defaultValue, Span<byte> dest)
        {
            dest.Clear();
            if (type is DbValueType.StrVar or DbValueType.BytesVar)
            {
                byte[] payload;
                if (!defaultValue.HasValue)
                {
                    payload = [];
                }
                else
                {
                    var cell = defaultValue.Value.WithColumnType(type);
                    payload = type == DbValueType.StrVar
                        ? Encoding.UTF8.GetBytes(cell.AsString())
                        : cell.AsBytes();
                }

                var link = DataVarManager.InsertDataVar(db, payload);
                var node = new NodeLink { TargetPageID = link.pageId, TargetNodeIndex = link.nodeIndex };
                node.ToBytesAndSpan(dest);
                return;
            }

            if (!defaultValue.HasValue)
                return;

            var typed = defaultValue.Value.WithColumnType(type);
            typed.Serialize(dest);
        }

        private static void ValidateDefault(DbValueType type, DbCell value)
        {
            try
            {
                var cell = value.WithColumnType(type);
                LumException.ThrowIfNotTrue(type.CheckType(in cell), $"Wrong type for new column {type}");
                cell.EnsureFitsColumn(type);
            }
            catch (ColumnAlterRejected)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ColumnAlterRejected(ex.Message);
            }
        }

        private static void Reject(bool failed, string message)
        {
            if (failed)
                throw new ColumnAlterRejected(message);
        }

        private static int CountLive(DbCache db, TablePage table)
        {
            int n = 0;
            if (!db.IsValidPage(table.PageHeader.RootDataPageId))
                return 0;

            uint pageId = table.PageHeader.RootDataPageId;
            while (db.IsValidPage(pageId))
            {
                var page = PageManager.GetPage<DataPage>(db, pageId);
                n += page.CurrentDataCount;
                pageId = page.NextPageId;
            }

            return n;
        }

        private static int[] Offsets(ColumnHeader[] headers)
        {
            var offsets = new int[headers.Length];
            int at = 0;
            for (int i = 0; i < headers.Length; i++)
            {
                offsets[i] = at;
                at += headers[i].ValueType.GetLength();
            }

            return offsets;
        }

        private static ColumnHeader CloneHeader(TablePage table, ColumnHeader source)
        {
            var header = new ColumnHeader(table)
            {
                IsKey = source.IsKey,
                ValueType = source.ValueType,
            };
            source.Name.CopyTo(header.Name, 0);
            return header;
        }

        private static ColumnHeader NewHeader(TablePage table, string name, DbValueType type, bool isKey)
        {
            var header = new ColumnHeader(table)
            {
                IsKey = isKey,
                ValueType = type,
            };
            Span<byte> span = stackalloc byte[ColumnHeader.NameLength];
            span.Clear();
            name.PaddingToBytes(span);
            span.CopyTo(header.Name);
            return header;
        }

        private static TablePage RequireTable(DbCache db, string tableName)
        {
            Reject(string.IsNullOrWhiteSpace(tableName), "Table name is empty");
            var table = TableRepoManager.GetTablePage(db, tableName);
            if (table == null)
                throw new ColumnAlterRejected("Table not found or null.");
            return table;
        }
    }

    internal sealed class ColumnAlterRejected : Exception
    {
        public ColumnAlterRejected(string message) : base(message)
        {
        }
    }
}
