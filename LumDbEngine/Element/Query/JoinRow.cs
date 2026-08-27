using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Query
{
    /// <summary>
    /// Up to three sides (A ⋈ B ⋈ C). <see cref="RowView"/> is a ref struct, so this is too.
    /// </summary>
    internal ref struct JoinRow
    {
        private RowView _v0, _v1, _v2;
        private uint _id0, _id1, _id2;

        public JoinRow(int side, ref RowView view, uint id)
        {
            _v0 = default;
            _v1 = default;
            _v2 = default;
            _id0 = _id1 = _id2 = 0;
            switch (side)
            {
                case 0:
                    _v0 = view;
                    _id0 = id;
                    break;
                case 1:
                    _v1 = view;
                    _id1 = id;
                    break;
                case 2:
                    _v2 = view;
                    _id2 = id;
                    break;
                default:
                    throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} Join is limited to 3 tables.");
            }
        }

        public JoinRow(ref RowView v0, uint i0, ref RowView v1, uint i1)
        {
            _v0 = v0;
            _id0 = i0;
            _v1 = v1;
            _id1 = i1;
            _v2 = default;
            _id2 = 0;
        }

        public JoinRow(ref RowView v0, uint i0, ref RowView v1, uint i1, ref RowView v2, uint i2)
        {
            _v0 = v0;
            _id0 = i0;
            _v1 = v1;
            _id1 = i1;
            _v2 = v2;
            _id2 = i2;
        }

        public void Set(int side, ref RowView view, uint id)
        {
            switch (side)
            {
                case 0:
                    _v0 = view;
                    _id0 = id;
                    break;
                case 1:
                    _v1 = view;
                    _id1 = id;
                    break;
                case 2:
                    _v2 = view;
                    _id2 = id;
                    break;
                default:
                    throw LumException.Raise($"{LumExceptionMessage.UnsupportedLinq} Join is limited to 3 tables.");
            }
        }

        public uint Id(int side) => side switch
        {
            0 => _id0,
            1 => _id1,
            _ => _id2,
        };

        public bool Match(int side, RowViewPredicate pred) => side switch
        {
            0 => pred(ref _v0),
            1 => pred(ref _v1),
            _ => pred(ref _v2),
        };

        public object? ReadColumn(int side, int ordinal, DbValueType type) => side switch
        {
            0 => ColumnExpr.ReadColumn(ref _v0, ordinal, type),
            1 => ColumnExpr.ReadColumn(ref _v1, ordinal, type),
            _ => ColumnExpr.ReadColumn(ref _v2, ordinal, type),
        };
    }
}
