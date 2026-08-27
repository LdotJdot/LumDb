using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;

namespace LumDbEngine.Element.Query
{
    internal readonly struct NormKey : IEquatable<NormKey>
    {
        private readonly object? _v;
        private readonly object?[]? _parts;

        public NormKey(object? v)
        {
            _v = v;
            _parts = null;
        }

        public NormKey(object?[] parts)
        {
            _v = null;
            _parts = parts;
        }

        public bool Equals(NormKey other)
        {
            if (_parts != null || other._parts != null)
            {
                if (_parts == null || other._parts == null || _parts.Length != other._parts.Length)
                    return false;
                for (int i = 0; i < _parts.Length; i++)
                {
                    if (RowValue.Compare(_parts[i], other._parts[i]) != 0)
                        return false;
                }

                return true;
            }

            return RowValue.Compare(_v, other._v) == 0;
        }

        public override bool Equals(object? obj) => obj is NormKey k && Equals(k);

        public override int GetHashCode()
        {
            if (_parts != null)
            {
                var h = new HashCode();
                foreach (var p in _parts)
                    h.Add(Hash1(p));
                return h.ToHashCode();
            }

            return Hash1(_v);
        }

        private static int Hash1(object? v)
        {
            if (v is null) return 0;
            if (v is string s) return StringComparer.Ordinal.GetHashCode(s);
            if (RowValue.IsNumber(v)) return RowValue.ToDecimal(v).GetHashCode();
            if (v is DateTime d) return d.GetHashCode();
            if (v is bool b) return b.GetHashCode();
            return v.GetHashCode();
        }
    }

    internal static class RelOpExecutor
    {
        public static List<uint[]> Execute(DbCache db, RelOp op)
        {
            return op switch
            {
                ScanOp s => WrapIds(ScanIds(db, s)),
                JoinOp j => ExecuteJoin(db, j, 0, uint.MaxValue),
                WindowOp w => ExecuteWindow(db, w),
                _ => [],
            };
        }

        private static List<uint[]> WrapIds(List<uint> ids)
        {
            var list = new List<uint[]>(ids.Count);
            foreach (var id in ids)
                list.Add([id]);
            return list;
        }

        private static List<uint[]> ExecuteWindow(DbCache db, WindowOp w)
        {
            if (w.Input is ScanOp scan)
            {
                var table = TableRepoManager.GetTablePage(db, scan.Table);
                if (table == null)
                    return [];
                var plan = new EntityQueryPlan
                {
                    Where = scan.Filter,
                    OrderBy = w.OrderBy,
                    OrderDescending = w.OrderDescending,
                    Skip = w.Skip,
                    Limit = w.Limit,
                    Backward = scan.Backward,
                };
                return WrapIds(EntityQueryExecutor.CollectIds(db, table, plan));
            }

            if (w.OrderBy == null && w.Input is JoinOp join)
                return ExecuteJoin(db, join, w.Skip, w.Limit);

            var all = Execute(db, w.Input);
            if (w.OrderBy != null)
            {
                var tables = w.OrderTables ?? Tables(w.Input);
                all.Sort((a, b) =>
                {
                    var ca = EvalOrder(db, tables, a, w.OrderBy);
                    var cb = EvalOrder(db, tables, b, w.OrderBy);
                    var c = RowValue.Compare(ca, cb);
                    return w.OrderDescending ? -c : c;
                });
            }

            return Slice(all, w.Skip, w.Limit);
        }

        private static List<uint[]> Slice(List<uint[]> all, uint skip, uint limit)
        {
            var list = new List<uint[]>();
            uint skipped = 0;
            uint taken = 0;
            foreach (var row in all)
            {
                if (skipped < skip)
                {
                    skipped++;
                    continue;
                }

                if (taken >= limit)
                    break;
                list.Add(row);
                taken++;
            }

            return list;
        }

        private static object? EvalOrder(DbCache db, string[] tables, uint[] ids, RowExpr orderBy)
        {
            if (tables.Length == 1)
            {
                if (!TryOpen(db, tables[0], ids[0], out var view, out var id, out var live0))
                    return null;
                var jr = new JoinRow(0, ref view, id);
                var key = orderBy.Eval(ref jr);
                GC.KeepAlive(live0);
                return key;
            }

            if (tables.Length >= 3 && ids.Length >= 3)
            {
                if (!TryOpen(db, tables[0], ids[0], out var v0, out var id0, out var live0))
                    return null;
                if (!TryOpen(db, tables[1], ids[1], out var v1, out var id1, out var live1))
                    return null;
                if (!TryOpen(db, tables[2], ids[2], out var v2, out var id2, out var live2))
                    return null;
                var jr = new JoinRow(ref v0, id0, ref v1, id1, ref v2, id2);
                var key = orderBy.Eval(ref jr);
                GC.KeepAlive(live0);
                GC.KeepAlive(live1);
                GC.KeepAlive(live2);
                return key;
            }

            if (tables.Length >= 2 && ids.Length >= 2)
            {
                if (!TryOpen(db, tables[0], ids[0], out var v0, out var id0, out var live0))
                    return null;
                if (!TryOpen(db, tables[1], ids[1], out var v1, out var id1, out var live1))
                    return null;
                var jr = new JoinRow(ref v0, id0, ref v1, id1);
                var key = orderBy.Eval(ref jr);
                GC.KeepAlive(live0);
                GC.KeepAlive(live1);
                return key;
            }

            return null;
        }

        private static string[] Tables(RelOp op) => op switch
        {
            ScanOp s => [s.Table],
            JoinOp j => [.. Tables(j.Left), j.Right.Table],
            WindowOp w => Tables(w.Input),
            _ => [],
        };

        private static List<uint> ScanIds(DbCache db, ScanOp scan)
        {
            var table = TableRepoManager.GetTablePage(db, scan.Table);
            if (table == null)
                return [];
            var plan = new EntityQueryPlan
            {
                Where = scan.Filter,
                Backward = scan.Backward,
            };
            return EntityQueryExecutor.CollectIds(db, table, plan);
        }

        private static List<uint[]> ExecuteJoin(DbCache db, JoinOp join, uint skip, uint limit)
        {
            Dictionary<NormKey, List<uint>>? hash = null;
            List<uint>? nestedInner = null;
            if (join.Equi.Count > 0)
                hash = BuildHash(db, join.Right, join.Equi, join.InnerSide);
            else
                nestedInner = CollectIdsOnSide(db, join.Right, join.InnerSide);

            if (join.Left is ScanOp leftScan)
                return ProbeScan(db, leftScan, join, hash, nestedInner, skip, limit);

            var leftTables = Tables(join.Left);
            var leftRows = Execute(db, join.Left);
            return ProbeTuples(db, leftTables, leftRows, join, hash, nestedInner, skip, limit);
        }

        private static List<uint> CollectIdsOnSide(DbCache db, ScanOp scan, int side)
        {
            var ids = new List<uint>();
            var table = TableRepoManager.GetTablePage(db, scan.Table);
            if (table == null || !db.IsValidPage(table.PageHeader.RootDataPageId))
                return ids;

            var root = PageManager.GetPage<DataPage>(db, table.PageHeader.RootDataPageId);
            DataManager.GoThrough(db, table.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                if (scan.Filter != null)
                {
                    var jr = new JoinRow(side, ref view, id);
                    if (!scan.Filter.EvalBool(ref jr))
                        return true;
                }

                ids.Add(id);
                return true;
            }, scan.Backward);
            return ids;
        }

        private static Dictionary<NormKey, List<uint>> BuildHash(DbCache db, ScanOp right, List<(RowExpr Left, RowExpr Right)> equi, int innerSide)
        {
            var dict = new Dictionary<NormKey, List<uint>>();
            var table = TableRepoManager.GetTablePage(db, right.Table);
            if (table == null || !db.IsValidPage(table.PageHeader.RootDataPageId))
                return dict;

            var root = PageManager.GetPage<DataPage>(db, table.PageHeader.RootDataPageId);
            DataManager.GoThrough(db, table.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                var jr = new JoinRow(innerSide, ref view, id);
                if (right.Filter != null && !right.Filter.EvalBool(ref jr))
                    return true;

                var key = MakeKey(equi, inner: true, ref jr);
                if (!dict.TryGetValue(key, out var list))
                {
                    list = [];
                    dict[key] = list;
                }

                list.Add(id);
                return true;
            }, backward: false);

            return dict;
        }

        private static NormKey MakeKey(List<(RowExpr Left, RowExpr Right)> equi, bool inner, ref JoinRow row)
        {
            if (equi.Count == 1)
            {
                var v = inner ? equi[0].Right.Eval(ref row) : equi[0].Left.Eval(ref row);
                return new NormKey(v);
            }

            var parts = new object?[equi.Count];
            for (int i = 0; i < equi.Count; i++)
                parts[i] = inner ? equi[i].Right.Eval(ref row) : equi[i].Left.Eval(ref row);
            return new NormKey(parts);
        }

        private static List<uint[]> ProbeScan(
            DbCache db,
            ScanOp left,
            JoinOp join,
            Dictionary<NormKey, List<uint>>? hash,
            List<uint>? nestedInner,
            uint skip,
            uint limit)
        {
            var outRows = new List<uint[]>();
            var table = TableRepoManager.GetTablePage(db, left.Table);
            if (table == null || !db.IsValidPage(table.PageHeader.RootDataPageId))
                return outRows;

            var innerTable = TableRepoManager.GetTablePage(db, join.Right.Table);
            var root = PageManager.GetPage<DataPage>(db, table.PageHeader.RootDataPageId);
            uint skipped = 0;
            uint taken = 0;

            DataManager.GoThrough(db, table.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                if (left.Filter != null)
                {
                    var side = new JoinRow(0, ref view, id);
                    if (!left.Filter.EvalBool(ref side))
                        return true;
                }

                var leftRow = new JoinRow(0, ref view, id);
                return ProbeOne(db, innerTable, join, hash, nestedInner, skip, limit, [id], ref leftRow, ref skipped, ref taken, outRows);
            }, left.Backward);

            return outRows;
        }

        private static List<uint[]> ProbeTuples(
            DbCache db,
            string[] leftTables,
            List<uint[]> leftRows,
            JoinOp join,
            Dictionary<NormKey, List<uint>>? hash,
            List<uint>? nestedInner,
            uint skip,
            uint limit)
        {
            var outRows = new List<uint[]>();
            if (leftTables.Length == 0)
                return outRows;

            var innerTable = TableRepoManager.GetTablePage(db, join.Right.Table);
            var outerTable = TableRepoManager.GetTablePage(db, leftTables[0]);
            uint skipped = 0;
            uint taken = 0;

            foreach (var tup in leftRows)
            {
                if (taken >= limit)
                    break;

                if (tup.Length >= 2 && leftTables.Length >= 2)
                {
                    var t0 = TableRepoManager.GetTablePage(db, leftTables[0]);
                    var t1 = TableRepoManager.GetTablePage(db, leftTables[1]);
                    if (t0 == null || t1 == null)
                        continue;
                    if (!TryOpen(db, t0, tup[0], out var v0, out var id0, out var live0))
                        continue;
                    if (!TryOpen(db, t1, tup[1], out var v1, out var id1, out var live1))
                        continue;
                    var left = new JoinRow(ref v0, id0, ref v1, id1);
                    var cont2 = ProbeOne(db, innerTable, join, hash, nestedInner, skip, limit, tup, ref left, ref skipped, ref taken, outRows);
                    GC.KeepAlive(live0);
                    GC.KeepAlive(live1);
                    if (!cont2)
                        break;
                    continue;
                }

                if (outerTable == null || !TryOpen(db, outerTable, tup[0], out var view, out var id, out var live))
                    continue;
                var left1 = new JoinRow(0, ref view, id);
                var cont = ProbeOne(db, innerTable, join, hash, nestedInner, skip, limit, tup, ref left1, ref skipped, ref taken, outRows);
                GC.KeepAlive(live);
                if (!cont)
                    break;
            }

            return outRows;
        }

        private static bool ProbeOne(
            DbCache db,
            TablePage? innerTable,
            JoinOp join,
            Dictionary<NormKey, List<uint>>? hash,
            List<uint>? nestedInner,
            uint skip,
            uint limit,
            uint[] leftTup,
            ref JoinRow left,
            ref uint skipped,
            ref uint taken,
            List<uint[]> outRows)
        {
            if (taken >= limit)
                return false;

            if (join.LeftFilter != null && !join.LeftFilter.EvalBool(ref left))
                return true;

            if (hash != null)
            {
                var key = MakeKey(join.Equi, inner: false, ref left);
                if (!hash.TryGetValue(key, out var inners))
                    return true;

                foreach (var innerId in inners)
                {
                    if (!ResidualOk(db, innerTable, join, ref left, innerId))
                        continue;
                    if (!Emit(join.Kind, leftTup, innerId, skip, limit, ref skipped, ref taken, outRows))
                        return false;
                    if (join.Kind == JoinKind.Semi)
                        return taken < limit;
                }

                return taken < limit;
            }

            if (nestedInner == null || innerTable == null)
                return true;

            foreach (var innerId in nestedInner)
            {
                if (!ResidualOk(db, innerTable, join, ref left, innerId))
                    continue;
                if (!Emit(join.Kind, leftTup, innerId, skip, limit, ref skipped, ref taken, outRows))
                    return false;
                if (join.Kind == JoinKind.Semi)
                    return taken < limit;
            }

            return taken < limit;
        }

        private static bool ResidualOk(DbCache db, TablePage? innerTable, JoinOp join, ref JoinRow left, uint innerId)
        {
            if (join.Residual == null)
                return true;
            if (innerTable == null || !TryOpen(db, innerTable, innerId, out var inner, out var iid, out var live))
                return false;
            var row = left;
            row.Set(join.InnerSide, ref inner, iid);
            var ok = join.Residual.EvalBool(ref row);
            GC.KeepAlive(live);
            return ok;
        }

        private static bool Emit(
            JoinKind kind,
            uint[] leftTup,
            uint innerId,
            uint skip,
            uint limit,
            ref uint skipped,
            ref uint taken,
            List<uint[]> outRows)
        {
            if (skipped < skip)
            {
                skipped++;
                return true;
            }

            if (taken >= limit)
                return false;

            if (kind == JoinKind.Semi)
                outRows.Add(leftTup);
            else
            {
                var row = new uint[leftTup.Length + 1];
                leftTup.CopyTo(row, 0);
                row[^1] = innerId;
                outRows.Add(row);
            }

            taken++;
            return taken < limit;
        }

        private static bool TryOpen(DbCache db, string tableName, uint id, out RowView view, out uint nodeId, out int[] offsets)
        {
            view = default;
            nodeId = 0;
            offsets = [];
            var table = TableRepoManager.GetTablePage(db, tableName);
            if (table == null)
                return false;
            return TryOpen(db, table, id, out view, out nodeId, out offsets);
        }

        private static bool TryOpen(DbCache db, TablePage table, uint id, out RowView view, out uint nodeId, out int[] offsets)
        {
            offsets = new int[table.ColumnHeaders.Length];
            DataManager.FillColumnOffsets(table.ColumnHeaders, offsets);
            var node = TableManager.FirstOrDefaultNode(db, table, id);
            if (node == null)
            {
                view = default;
                nodeId = 0;
                return false;
            }

            view = new RowView(node.Data, table.ColumnHeaders, offsets, db);
            nodeId = node.Id;
            return true;
        }
    }
}
