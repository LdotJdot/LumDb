using System.Linq.Expressions;
using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Manager.Specific;

namespace LumDbEngine.Element.Query
{
    internal static class RelOpFactory
    {
        public static RelOp TwoTable(
            DbCache db,
            string t0,
            string t1,
            OuterSnap outer,
            LambdaExpression on,
            List<LambdaExpression> wheres,
            int skip,
            int take,
            LambdaExpression? orderBy,
            bool orderDescending,
            JoinKind kind = JoinKind.Inner)
        {
            var p0 = TableRepoManager.GetTablePage(db, t0);
            var p1 = TableRepoManager.GetTablePage(db, t1);
            if (p0 == null || p1 == null)
                return new ScanOp { Table = t0, Filter = new ConstExpr(false) };

            var m0 = EntityColumnMap.FromHeaders(p0.ColumnHeaders);
            var m1 = EntityColumnMap.FromHeaders(p1.ColumnHeaders);
            var maps = new[] { m0, m1 };

            RowExpr? pred = LinqToRowExpr.Translate(on, maps);
            foreach (var w in wheres)
                pred = RowExpr.And(pred, LinqToRowExpr.Translate(w, maps));
            var split = PredSplit.Split(pred, innerSide: 1);

            RowExpr? outerWhere = null;
            foreach (var w in outer.Wheres)
                outerWhere = RowExpr.And(outerWhere, LinqToRowExpr.Translate(w, m0));
            foreach (var e in outer.Extra)
                outerWhere = RowExpr.And(outerWhere, e);
            outerWhere = RowExpr.And(outerWhere, split.OuterOnly);

            RelOp left = new ScanOp
            {
                Table = t0,
                Filter = outerWhere,
                Backward = outer.Backward && outer.OrderBy == null,
            };

            if (outer.OrderBy != null || outer.Skip != 0 || outer.Take != int.MaxValue)
            {
                left = new WindowOp
                {
                    Input = left,
                    Skip = (uint)outer.Skip,
                    Limit = outer.Take == int.MaxValue ? uint.MaxValue : (uint)outer.Take,
                    OrderBy = outer.OrderBy == null ? null : LinqToRowExpr.Translate(outer.OrderBy, m0),
                    OrderDescending = outer.OrderDescending,
                    OrderTables = [t0],
                };
            }

            RelOp join = new JoinOp
            {
                Left = left,
                Right = new ScanOp { Table = t1, Filter = split.InnerOnly },
                Equi = split.Equi,
                Residual = split.Residual,
                Kind = kind,
                InnerSide = 1,
            };

            return new WindowOp
            {
                Input = join,
                Skip = (uint)skip,
                Limit = take == int.MaxValue ? uint.MaxValue : (uint)take,
                OrderBy = orderBy == null ? null : LinqToRowExpr.Translate(orderBy, maps),
                OrderDescending = orderDescending,
                OrderTables = [t0, t1],
            };
        }

        public static RelOp ThreeTable(
            DbCache db,
            string t0,
            string t1,
            string t2,
            OuterSnap outer,
            LambdaExpression on1,
            List<LambdaExpression> wheres2,
            int skip2,
            int take2,
            LambdaExpression? order2,
            bool desc2,
            LambdaExpression on2,
            List<LambdaExpression> wheres3,
            int skip3,
            int take3,
            LambdaExpression? order3,
            bool desc3)
        {
            var left = TwoTable(db, t0, t1, outer, on1, wheres2, skip2, take2, order2, desc2);
            var p0 = TableRepoManager.GetTablePage(db, t0);
            var p1 = TableRepoManager.GetTablePage(db, t1);
            var p2 = TableRepoManager.GetTablePage(db, t2);
            if (p0 == null || p1 == null || p2 == null)
                return new ScanOp { Table = t0, Filter = new ConstExpr(false) };

            var maps = new[]
            {
                EntityColumnMap.FromHeaders(p0.ColumnHeaders),
                EntityColumnMap.FromHeaders(p1.ColumnHeaders),
                EntityColumnMap.FromHeaders(p2.ColumnHeaders),
            };

            RowExpr? pred = LinqToRowExpr.Translate(on2, maps);
            foreach (var w in wheres3)
                pred = RowExpr.And(pred, LinqToRowExpr.Translate(w, maps));
            var split = PredSplit.Split(pred, innerSide: 2);

            RelOp join = new JoinOp
            {
                Left = left,
                Right = new ScanOp { Table = t2, Filter = split.InnerOnly },
                Equi = split.Equi,
                Residual = split.Residual,
                LeftFilter = split.OuterOnly,
                Kind = JoinKind.Inner,
                InnerSide = 2,
            };

            return new WindowOp
            {
                Input = join,
                Skip = (uint)skip3,
                Limit = take3 == int.MaxValue ? uint.MaxValue : (uint)take3,
                OrderBy = order3 == null ? null : LinqToRowExpr.Translate(order3, maps),
                OrderDescending = desc3,
                OrderTables = [t0, t1, t2],
            };
        }
    }
}
