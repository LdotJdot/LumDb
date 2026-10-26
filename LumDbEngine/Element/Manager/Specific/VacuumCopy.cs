using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Utils.StringUtils;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;
using System.Text;

namespace LumDbEngine.Element.Manager.Specific
{
    /// <summary>
    /// Rebuilds every live table into an empty destination cache.
    /// Fixed columns are copied as raw bytes. Variable columns are read and written
    /// again so dead var nodes and free pages are left behind. Row ids are preserved.
    /// </summary>
    internal static class VacuumCopy
    {
        public static void Copy(DbCache source, DbCache destination)
        {
            foreach (var node in TableRepoManager.IterateNodes(source))
            {
                if (node.TargetLink.TargetPageID == 0)
                    continue;

                var table = PageManager.GetPage<TablePage>(source, node.TargetLink.TargetPageID);
                var name = node.KeyToString().TrimEnd('\0');
                var schema = new (string columnName, DbValueType type, bool isKey)[table.ColumnHeaders.Length];
                for (int i = 0; i < table.ColumnHeaders.Length; i++)
                {
                    var header = table.ColumnHeaders[i];
                    schema[i] = (header.Name.TransformToToString(), header.ValueType, header.IsKey);
                }

                var created = new DbManager().Create(destination, name, schema);
                if (!created.IsSuccess)
                    throw created.Exception ?? LumException.Raise("Vacuum failed to create " + name);

                var destTable = TableRepoManager.GetTablePage(destination, name);
                LumException.ThrowIfNull(destTable, "Vacuum lost table " + name);

                uint nextId = table.PageHeader.NextDataId;
                if (!source.IsValidPage(table.PageHeader.RootDataPageId))
                {
                    destTable.PageHeader.NextDataId = nextId;
                    destTable.MarkDirty();
                    continue;
                }

                var offsets = new int[table.ColumnHeaders.Length];
                int at = 0;
                for (int i = 0; i < table.ColumnHeaders.Length; i++)
                {
                    offsets[i] = at;
                    at += table.ColumnHeaders[i].ValueType.GetLength();
                }

                uint pageId = table.PageHeader.RootDataPageId;
                while (source.IsValidPage(pageId))
                {
                    var page = PageManager.GetPage<DataPage>(source, pageId);
                    for (int i = 0; i < page.MaxDataCount; i++)
                    {
                        var rowNode = page.DataNodes[i];
                        if (!rowNode.IsAvailable)
                            continue;

                        var raw = rowNode.Data.ToArray();
                        var id = rowNode.Id;
                        for (int c = 0; c < table.ColumnHeaders.Length; c++)
                        {
                            var type = table.ColumnHeaders[c].ValueType;
                            if ((byte)type <= DbValueTypeUtils.DataVarSplitter)
                                continue;

                            NodeLink.Create(raw.AsSpan(offsets[c], NodeLink.Size), out var link);
                            byte[] payload = source.IsValidPage(link.TargetPageID)
                                ? DataVarManager.GetDataVar(source, link)
                                : [];
                            var fresh = DataVarManager.InsertDataVar(destination, payload);
                            var rewritten = new NodeLink
                            {
                                TargetPageID = fresh.pageId,
                                TargetNodeIndex = fresh.nodeIndex,
                            };
                            rewritten.ToBytesAndSpan(raw.AsSpan(offsets[c], NodeLink.Size));
                        }

                        var inserted = DataManager.InsertPreservedRow(destination, destTable, raw, id);
                        IndexManager.InsertMainIndex(destination, destTable, inserted);
                        for (int c = 0; c < destTable.ColumnHeaders.Length; c++)
                        {
                            if (!destTable.ColumnHeaders[c].IsKey)
                                continue;
                            IndexManager.InsertSubIndex(destination, destTable, inserted, destTable.ColumnHeaders[c].Name.TransformToToString());
                        }
                    }

                    pageId = page.NextPageId;
                }

                destTable.PageHeader.NextDataId = nextId;
                destTable.MarkDirty();
            }
        }
    }
}
