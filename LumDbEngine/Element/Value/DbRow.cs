using System.Runtime.CompilerServices;
using System.Text;
using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Utils.ByteUtils;

namespace LumDbEngine.Element.Structure
{
    /// <summary>
    /// Typed row accessor over materialized <see cref="DbCell"/> values (safe to return from APIs).
    /// </summary>
    public interface IDbRow
    {
        int ColumnCount { get; }
        DbCell GetCell(int ordinal);
        bool GetBool(int ordinal);
        byte GetByte(int ordinal);
        int GetInt(int ordinal);
        uint GetUInt(int ordinal);
        long GetLong(int ordinal);
        ulong GetULong(int ordinal);
        float GetFloat(int ordinal);
        double GetDouble(int ordinal);
        decimal GetDecimal(int ordinal);
        DateTime GetDateTimeUtc(int ordinal);
        string GetString(int ordinal);
        byte[] GetBytes(int ordinal);

        /// <summary>Materialize boxed column values (compatibility).</summary>
        object[] ToObjectArray();
    }

    /// <summary>
    /// Heap-friendly row holding <see cref="DbCell"/> cells.
    /// </summary>
    public sealed class DbRow : IDbRow
    {
        private readonly DbCell[] _cells;

        public DbRow(DbCell[] cells)
        {
            _cells = cells;
        }

        public int ColumnCount => _cells.Length;
        public DbCell GetCell(int ordinal) => _cells[ordinal];
        public bool GetBool(int ordinal) => _cells[ordinal].AsBool();
        public byte GetByte(int ordinal) => _cells[ordinal].AsByte();
        public int GetInt(int ordinal) => _cells[ordinal].AsInt();
        public uint GetUInt(int ordinal) => _cells[ordinal].AsUInt();
        public long GetLong(int ordinal) => _cells[ordinal].AsLong();
        public ulong GetULong(int ordinal) => _cells[ordinal].AsULong();
        public float GetFloat(int ordinal) => _cells[ordinal].AsFloat();
        public double GetDouble(int ordinal) => _cells[ordinal].AsDouble();
        public decimal GetDecimal(int ordinal) => _cells[ordinal].AsDecimal();
        public DateTime GetDateTimeUtc(int ordinal) => _cells[ordinal].AsDateTimeUtc();
        public string GetString(int ordinal) => _cells[ordinal].AsString();
        public byte[] GetBytes(int ordinal) => _cells[ordinal].AsBytes();

        public object[] ToObjectArray()
        {
            var arr = new object[_cells.Length];
            for (int i = 0; i < _cells.Length; i++)
                arr[i] = _cells[i].ToObject();
            return arr;
        }

        public DbCell[] Cells => _cells;
    }

    /// <summary>
    /// Owned row copy for Find results: scalars decode on demand from bytes; var columns are resolved at construction.
    /// </summary>
    public sealed class DbRowBuffer : IDbRow
    {
        private readonly byte[] _data;
        private readonly DbValueType[] _types;
        private readonly int[] _offsets;
        private readonly object?[] _varPayload; // string or byte[] for var columns; null for fixed
        private DbCell[]? _cellCache;

        internal DbRowBuffer(byte[] data, DbValueType[] types, int[] offsets, object?[] varPayload)
        {
            _data = data;
            _types = types;
            _offsets = offsets;
            _varPayload = varPayload;
        }

        public int ColumnCount => _types.Length;

        private Span<byte> Slice(int ordinal)
            => _data.AsSpan(_offsets[ordinal], _types[ordinal].GetLength());

        private void EnsureFixed(int ordinal, DbValueType expected)
        {
            if (_types[ordinal] != expected)
                throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: expected {expected}, got {_types[ordinal]}");
        }

        public bool GetBool(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.Bool);
            return BitConverter.ToBoolean(Slice(ordinal));
        }

        public byte GetByte(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.Byte);
            return Slice(ordinal)[0];
        }

        public int GetInt(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.Int);
            return BitConverter.ToInt32(Slice(ordinal));
        }

        public uint GetUInt(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.UInt);
            return BitConverter.ToUInt32(Slice(ordinal));
        }

        public long GetLong(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.Long);
            return BitConverter.ToInt64(Slice(ordinal));
        }

        public ulong GetULong(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.ULong);
            return BitConverter.ToUInt64(Slice(ordinal));
        }

        public float GetFloat(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.Float);
            return BitConverter.ToSingle(Slice(ordinal));
        }

        public double GetDouble(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.Double);
            return BitConverter.ToDouble(Slice(ordinal));
        }

        public decimal GetDecimal(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.Decimal);
            return BytesConverter.ToDecimalNoAlloc(Slice(ordinal));
        }

        public DateTime GetDateTimeUtc(int ordinal)
        {
            EnsureFixed(ordinal, DbValueType.DateTimeUTC);
            return new DateTime(BitConverter.ToInt64(Slice(ordinal)), DateTimeKind.Utc);
        }

        public string GetString(int ordinal)
        {
            var type = _types[ordinal];
            if (type == DbValueType.StrVar)
                return (string)_varPayload[ordinal]!;
            if (type is DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B)
                return Encoding.UTF8.GetString(Slice(ordinal)).TrimEnd('\0');
            throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {type}");
        }

        public byte[] GetBytes(int ordinal)
        {
            var type = _types[ordinal];
            if (type == DbValueType.BytesVar)
                return (byte[])_varPayload[ordinal]!;
            if (type is DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32)
                return Slice(ordinal).ToArray();
            throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {type}");
        }

        public DbCell GetCell(int ordinal)
        {
            _cellCache ??= BuildCells();
            return _cellCache[ordinal];
        }

        public object[] ToObjectArray()
        {
            var arr = new object[_types.Length];
            for (int i = 0; i < _types.Length; i++)
                arr[i] = GetCell(i).ToObject();
            return arr;
        }

        private DbCell[] BuildCells()
        {
            var cells = new DbCell[_types.Length];
            for (int i = 0; i < _types.Length; i++)
            {
                var type = _types[i];
                cells[i] = type switch
                {
                    DbValueType.StrVar => DbCell.FromString((string)_varPayload[i]!, type),
                    DbValueType.BytesVar => DbCell.FromBytes((byte[])_varPayload[i]!, type),
                    _ => DbCell.Deserialize(Slice(i), type),
                };
            }
            return cells;
        }
    }

    /// <summary>
    /// Zero-allocation (for scalar reads) cursor over a raw row buffer.
    /// Constructed only by the engine during <see cref="RowViewAction"/> callbacks.
    /// </summary>
    public ref struct RowView
    {
        private readonly Span<byte> _row;
        private readonly ColumnHeader[] _headers;
        private readonly ReadOnlySpan<int> _offsets;
        private readonly DbCache _db;

        internal RowView(Span<byte> row, ColumnHeader[] headers, ReadOnlySpan<int> offsets, DbCache db)
        {
            _row = row;
            _headers = headers;
            _offsets = offsets;
            _db = db;
        }

        public int ColumnCount => _headers.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> Slice(int ordinal)
            => _row.Slice(_offsets[ordinal], _headers[ordinal].ValueType.GetLength());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Ensure(int ordinal, DbValueType expected)
        {
            if (_headers[ordinal].ValueType != expected)
                throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: expected {expected}, got {_headers[ordinal].ValueType}");
        }

        public DbCell GetCell(int ordinal)
            => DbCell.Deserialize(Slice(ordinal), _db, _headers[ordinal].ValueType);

        public bool GetBool(int ordinal)
        {
            Ensure(ordinal, DbValueType.Bool);
            return BitConverter.ToBoolean(Slice(ordinal));
        }

        public byte GetByte(int ordinal)
        {
            Ensure(ordinal, DbValueType.Byte);
            return Slice(ordinal)[0];
        }

        public int GetInt(int ordinal)
        {
            Ensure(ordinal, DbValueType.Int);
            return BitConverter.ToInt32(Slice(ordinal));
        }

        public uint GetUInt(int ordinal)
        {
            Ensure(ordinal, DbValueType.UInt);
            return BitConverter.ToUInt32(Slice(ordinal));
        }

        public long GetLong(int ordinal)
        {
            Ensure(ordinal, DbValueType.Long);
            return BitConverter.ToInt64(Slice(ordinal));
        }

        public ulong GetULong(int ordinal)
        {
            Ensure(ordinal, DbValueType.ULong);
            return BitConverter.ToUInt64(Slice(ordinal));
        }

        public float GetFloat(int ordinal)
        {
            Ensure(ordinal, DbValueType.Float);
            return BitConverter.ToSingle(Slice(ordinal));
        }

        public double GetDouble(int ordinal)
        {
            Ensure(ordinal, DbValueType.Double);
            return BitConverter.ToDouble(Slice(ordinal));
        }

        public decimal GetDecimal(int ordinal)
        {
            Ensure(ordinal, DbValueType.Decimal);
            return BytesConverter.ToDecimalNoAlloc(Slice(ordinal));
        }

        public DateTime GetDateTimeUtc(int ordinal)
        {
            Ensure(ordinal, DbValueType.DateTimeUTC);
            return new DateTime(BitConverter.ToInt64(Slice(ordinal)), DateTimeKind.Utc);
        }

        public string GetString(int ordinal)
        {
            var type = _headers[ordinal].ValueType;
            if (type is DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B)
                return Encoding.UTF8.GetString(Slice(ordinal)).TrimEnd('\0');
            if (type == DbValueType.StrVar)
                return DbCell.Deserialize(Slice(ordinal), _db, type).AsString();
            throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {type}");
        }

        public byte[] GetBytes(int ordinal)
        {
            var type = _headers[ordinal].ValueType;
            if (type is DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32)
                return Slice(ordinal).ToArray();
            if (type == DbValueType.BytesVar)
                return DbCell.Deserialize(Slice(ordinal), _db, type).AsBytes();
            throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {type}");
        }

        public DbCell this[int ordinal] => GetCell(ordinal);

        internal Span<byte> RowSpan => _row;
        internal ColumnHeader[] Headers => _headers;
        internal DbCache Cache => _db;
    }

    /// <summary>Callback for zero-allocation scalar scan over <see cref="RowView"/>.</summary>
    public delegate bool RowViewAction(ref RowView row);

    /// <summary>RowView scan that also receives the engine row id.</summary>
    public delegate bool RowViewIdAction(uint id, ref RowView row);

    /// <summary>Predicate over <see cref="RowView"/> (no full-row materialization).</summary>
    public delegate bool RowViewPredicate(ref RowView row);

    /// <summary>Writes entity fields in table column order into a cell buffer.</summary>
    public ref struct RowWriter
    {
        private readonly DbCell[] _cells;
        private int _index;

        public RowWriter(DbCell[] cells)
        {
            _cells = cells;
            _index = 0;
        }

        public void Write(DbCell cell)
        {
            _cells[_index++] = cell;
        }

        public void WriteBool(bool v) => Write(v);
        public void WriteByte(byte v) => Write(v);
        public void WriteInt(int v) => Write(v);
        public void WriteUInt(uint v) => Write(v);
        public void WriteLong(long v) => Write(v);
        public void WriteULong(ulong v) => Write(v);
        public void WriteFloat(float v) => Write(v);
        public void WriteDouble(double v) => Write(v);
        public void WriteDecimal(decimal v) => Write(v);
        public void WriteDateTimeUtc(DateTime v) => Write(v);
        public void WriteString(string v) => Write(v);
        public void WriteString(string v, DbValueType stringType) => Write(DbCell.FromString(v, stringType));
        public void WriteBytes(byte[] v) => Write(v);
        public void WriteBytes(byte[] v, DbValueType bytesType) => Write(DbCell.FromBytes(v, bytesType));
    }
}
