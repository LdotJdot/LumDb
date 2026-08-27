namespace LumDbEngine.Element.Query
{
    internal enum JoinKind
    {
        Inner,
        Semi,
    }

    /// <summary>
    /// Independent relational operators. Compose: Window(Join(Scan, Scan)), Join(Window(Scan), Scan), …
    /// Page walk is always occupancy-order GoThrough; this layer does not touch page linking.
    /// </summary>
    internal abstract class RelOp
    {
    }

    internal sealed class ScanOp : RelOp
    {
        public required string Table { get; init; }
        public RowExpr? Filter { get; init; }
        public bool Backward { get; init; }
    }

    internal sealed class JoinOp : RelOp
    {
        public required RelOp Left { get; init; }
        public required ScanOp Right { get; init; }
        public required List<(RowExpr Left, RowExpr Right)> Equi { get; init; }
        public RowExpr? Residual { get; init; }
        public RowExpr? LeftFilter { get; init; }
        public JoinKind Kind { get; init; }
        public int InnerSide { get; init; } = 1;
    }

    internal sealed class WindowOp : RelOp
    {
        public required RelOp Input { get; init; }
        public uint Skip { get; init; }
        public uint Limit { get; init; } = uint.MaxValue;
        public RowExpr? OrderBy { get; init; }
        public bool OrderDescending { get; init; }
        public string[]? OrderTables { get; init; }
    }
}
