using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Engine.Results;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page.Data;
using LumDbEngine.Element.Structure.Page.Key;

namespace LumDbEngine.Element.Query
{
    internal sealed class EntityQueryPlan
    {
        public RowExpr? Where { get; set; }
        public RowExpr? OrderBy { get; set; }
        public bool OrderDescending { get; set; }
        public uint Skip { get; set; }
        public uint Limit { get; set; } = uint.MaxValue;
        public bool Backward { get; set; }
    }

    internal static class EntityQueryExecutor
    {
        public static IDbValues<T> Execute<T>(
            DbCache db,
            TablePage tablePage,
            EntityQueryPlan plan,
            Func<uint, IDbRow, T> fromRow,
            Func<uint, T?> fromId)
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return new DbValues<T>([]);

            var root = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);

            if (plan.OrderBy != null)
                return ExecuteOrdered(db, tablePage, root, plan, fromRow, fromId);

            return ExecuteWindow(db, tablePage, root, plan, fromRow);
        }

        /// <summary>
        /// Occupancy/order window of matching row ids. Does not materialize entities.
        /// Callers must delete after the scan (do not mutate the page chain while walking).
        /// </summary>
        public static List<uint> CollectIds(
            DbCache db,
            TablePage tablePage,
            EntityQueryPlan plan)
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return [];

            var root = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            if (plan.OrderBy != null)
                return CollectIdsOrdered(db, tablePage, root, plan);
            return CollectIdsWindow(db, tablePage, root, plan);
        }

        private static List<uint> CollectIdsWindow(
            DbCache db,
            TablePage tablePage,
            DataPage? root,
            EntityQueryPlan plan)
        {
            var ids = new List<uint>();
            uint skipped = 0;
            uint taken = 0;
            var backward = plan.Backward && plan.OrderBy == null;

            DataManager.GoThrough(db, tablePage.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                if (!Matches(plan.Where, ref view, id))
                    return true;
                if (skipped < plan.Skip)
                {
                    skipped++;
                    return true;
                }
                if (taken >= plan.Limit)
                    return false;
                ids.Add(id);
                taken++;
                return taken < plan.Limit;
            }, backward);

            return ids;
        }

        private static List<uint> CollectIdsOrdered(
            DbCache db,
            TablePage tablePage,
            DataPage? root,
            EntityQueryPlan plan)
        {
            var keys = new List<(object? Key, uint Id)>();
            DataManager.GoThrough(db, tablePage.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                if (!Matches(plan.Where, ref view, id))
                    return true;
                keys.Add((plan.OrderBy!.Eval(ref view, id), id));
                return true;
            }, backward: false);

            keys.Sort((a, b) =>
            {
                var c = RowValue.Compare(a.Key, b.Key);
                return plan.OrderDescending ? -c : c;
            });

            var ids = new List<uint>();
            uint skipped = 0;
            uint taken = 0;
            foreach (var item in keys)
            {
                if (skipped < plan.Skip)
                {
                    skipped++;
                    continue;
                }
                if (taken >= plan.Limit)
                    break;
                ids.Add(item.Id);
                taken++;
            }

            return ids;
        }

        public static uint Count(
            DbCache db,
            TablePage tablePage,
            RowExpr? where)
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return 0;

            var root = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            uint n = 0;
            DataManager.GoThrough(db, tablePage.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                if (Matches(where, ref view, id))
                    n++;
                return true;
            }, backward: false);
            return n;
        }

        public static bool Exists(
            DbCache db,
            TablePage tablePage,
            RowExpr? where,
            bool backward)
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return false;

            var root = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            var any = false;
            DataManager.GoThrough(db, tablePage.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                if (!Matches(where, ref view, id))
                    return true;
                any = true;
                return false;
            }, backward);
            return any;
        }

        public static uint? FindFirstId(
            DbCache db,
            TablePage tablePage,
            RowExpr where)
        {
            if (!db.IsValidPage(tablePage.PageHeader.RootDataPageId))
                return null;

            var root = PageManager.GetPage<DataPage>(db, tablePage.PageHeader.RootDataPageId);
            uint found = 0;
            DataManager.GoThrough(db, tablePage.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                if (!Matches(where, ref view, id))
                    return true;
                found = id;
                return false;
            }, backward: false);
            return found == 0 ? null : found;
        }

        private static IDbValues<T> ExecuteWindow<T>(
            DbCache db,
            TablePage tablePage,
            DataPage? root,
            EntityQueryPlan plan,
            Func<uint, IDbRow, T> fromRow)
        {
            var list = new List<T>();
            uint skipped = 0;
            uint taken = 0;
            var backward = plan.Backward && plan.OrderBy == null;

            DataManager.GoThrough(db, tablePage.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                if (!Matches(plan.Where, ref view, id))
                    return true;

                if (skipped < plan.Skip)
                {
                    skipped++;
                    return true;
                }

                if (taken >= plan.Limit)
                    return false;

                var row = DataManager.CreateRowBuffer(db, tablePage.ColumnHeaders, view.RowSpan);
                list.Add(fromRow(id, row));
                taken++;
                return taken < plan.Limit;
            }, backward);

            return new DbValues<T>(list);
        }

        private static IDbValues<T> ExecuteOrdered<T>(
            DbCache db,
            TablePage tablePage,
            DataPage? root,
            EntityQueryPlan plan,
            Func<uint, IDbRow, T> fromRow,
            Func<uint, T?> fromId)
        {
            var keys = new List<(object? Key, uint Id)>();
            DataManager.GoThrough(db, tablePage.ColumnHeaders, root, (uint id, ref RowView view) =>
            {
                if (!Matches(plan.Where, ref view, id))
                    return true;
                keys.Add((plan.OrderBy!.Eval(ref view, id), id));
                return true;
            }, backward: false);

            keys.Sort((a, b) =>
            {
                var c = RowValue.Compare(a.Key, b.Key);
                return plan.OrderDescending ? -c : c;
            });

            var list = new List<T>();
            uint skipped = 0;
            uint taken = 0;
            foreach (var item in keys)
            {
                if (skipped < plan.Skip)
                {
                    skipped++;
                    continue;
                }

                if (taken >= plan.Limit)
                    break;

                var entity = fromId(item.Id);
                if (entity != null)
                {
                    list.Add(entity);
                    taken++;
                }
            }

            return new DbValues<T>(list);
        }

        private static bool Matches(RowExpr? where, ref RowView view, uint id)
            => where == null || where.EvalBool(ref view, id);
    }
}
