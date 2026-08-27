namespace LumDbEngine.Element.Query
{
    /// <summary>
    /// Splits a join predicate: left-only Filter, inner-only Filter, equi-keys, residual.
    /// <paramref name="innerSide"/> is 1 for A⋈B and 2 for (A⋈B)⋈C.
    /// </summary>
    internal sealed class PredSplit
    {
        public RowExpr? OuterOnly { get; init; }
        public RowExpr? InnerOnly { get; init; }
        public List<(RowExpr Left, RowExpr Right)> Equi { get; } = new();
        public RowExpr? Residual { get; init; }

        public static PredSplit Split(RowExpr? expr, int innerSide = 1)
        {
            var innerBit = 1 << innerSide;
            var leftBits = innerBit - 1;
            var parts = new List<RowExpr>();
            FlattenAnd(expr, parts);

            RowExpr? outer = null;
            RowExpr? inner = null;
            RowExpr? residual = null;
            var equi = new List<(RowExpr, RowExpr)>();

            foreach (var part in parts)
            {
                if (part is ConstExpr { Value: true })
                    continue;
                if (part is ConstExpr { Value: false })
                    return new PredSplit { OuterOnly = new ConstExpr(false) };

                if (TryEqui(part, leftBits, innerBit, out var pair))
                {
                    equi.Add(pair);
                    continue;
                }

                var mask = part.SideMask;
                if (mask != 0 && (mask & innerBit) == 0)
                {
                    outer = RowExpr.And(outer, part);
                    continue;
                }

                if (mask != 0 && (mask & leftBits) == 0)
                {
                    inner = RowExpr.And(inner, part);
                    continue;
                }

                residual = RowExpr.And(residual, part);
            }

            var split = new PredSplit
            {
                OuterOnly = outer,
                InnerOnly = inner,
                Residual = residual,
            };
            split.Equi.AddRange(equi);
            return split;
        }

        private static bool TryEqui(RowExpr part, int leftBits, int innerBit, out (RowExpr Left, RowExpr Right) pair)
        {
            pair = default;
            if (part is not BinaryRowExpr { Op: RowOp.Equal } eq)
                return false;

            var lm = eq.Left.SideMask;
            var rm = eq.Right.SideMask;
            if (lm == 0 || rm == 0 || (lm & rm) != 0)
                return false;

            if (IsSubset(lm, leftBits) && IsSubset(rm, innerBit))
            {
                pair = (eq.Left, eq.Right);
                return true;
            }

            if (IsSubset(rm, leftBits) && IsSubset(lm, innerBit))
            {
                pair = (eq.Right, eq.Left);
                return true;
            }

            return false;
        }

        private static bool IsSubset(int mask, int bits) => mask != 0 && (mask & bits) == mask;

        private static void FlattenAnd(RowExpr? expr, List<RowExpr> dst)
        {
            if (expr == null)
                return;
            if (expr is BinaryRowExpr { Op: RowOp.And } and)
            {
                FlattenAnd(and.Left, dst);
                FlattenAnd(and.Right, dst);
                return;
            }

            dst.Add(expr);
        }
    }
}
