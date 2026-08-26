using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Utils.ByteUtils;

namespace LumDbEngine.Element.Structure
{
    /// <summary>
    /// Tagged union for a single column value without boxing scalars.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct DbCell
    {
        [FieldOffset(0)] private DbValueType _type;
        [FieldOffset(8)] private long _long0;
        [FieldOffset(16)] private long _long1;
        [FieldOffset(8)] private decimal _decimal;
        [FieldOffset(24)] private object? _ref;

        public DbValueType Type => _type;

        public static DbCell FromBool(bool v) { var c = new DbCell { _type = DbValueType.Bool, _long0 = v ? 1 : 0 }; return c; }
        public static DbCell FromByte(byte v) { var c = new DbCell { _type = DbValueType.Byte, _long0 = v }; return c; }
        public static DbCell FromInt(int v) { var c = new DbCell { _type = DbValueType.Int, _long0 = v }; return c; }
        public static DbCell FromUInt(uint v) { var c = new DbCell { _type = DbValueType.UInt, _long0 = v }; return c; }
        public static DbCell FromLong(long v) { var c = new DbCell { _type = DbValueType.Long, _long0 = v }; return c; }
        public static DbCell FromULong(ulong v) { var c = new DbCell { _type = DbValueType.ULong, _long0 = unchecked((long)v) }; return c; }
        public static DbCell FromFloat(float v) { var c = new DbCell { _type = DbValueType.Float }; Unsafe.As<long, float>(ref c._long0) = v; return c; }
        public static DbCell FromDouble(double v) { var c = new DbCell { _type = DbValueType.Double }; Unsafe.As<long, double>(ref c._long0) = v; return c; }
        public static DbCell FromDecimal(decimal v) { var c = new DbCell { _type = DbValueType.Decimal, _decimal = v }; return c; }
        public static DbCell FromDateTimeUtc(DateTime v)
        {
            if (v.Kind != DateTimeKind.Utc) throw LumException.Raise(LumExceptionMessage.DateTimeUtcError);
            return new DbCell { _type = DbValueType.DateTimeUTC, _long0 = v.Ticks };
        }
        public static DbCell FromString(string v, DbValueType stringType = DbValueType.StrVar)
            => new DbCell { _type = stringType, _ref = v ?? throw LumException.Raise(LumExceptionMessage.UnknownValType) };
        public static DbCell FromBytes(byte[] v, DbValueType bytesType = DbValueType.BytesVar)
            => new DbCell { _type = bytesType, _ref = v ?? throw LumException.Raise(LumExceptionMessage.UnknownValType) };

        public static implicit operator DbCell(bool v) => FromBool(v);
        public static implicit operator DbCell(byte v) => FromByte(v);
        public static implicit operator DbCell(int v) => FromInt(v);
        public static implicit operator DbCell(uint v) => FromUInt(v);
        public static implicit operator DbCell(long v) => FromLong(v);
        public static implicit operator DbCell(ulong v) => FromULong(v);
        public static implicit operator DbCell(float v) => FromFloat(v);
        public static implicit operator DbCell(double v) => FromDouble(v);
        public static implicit operator DbCell(decimal v) => FromDecimal(v);
        public static implicit operator DbCell(DateTime v) => FromDateTimeUtc(v);
        public static implicit operator DbCell(string v) => FromString(v);
        public static implicit operator DbCell(byte[] v) => FromBytes(v);

        public bool AsBool() { Ensure(DbValueType.Bool); return _long0 != 0; }
        public byte AsByte() { Ensure(DbValueType.Byte); return (byte)_long0; }
        public int AsInt() { Ensure(DbValueType.Int); return (int)_long0; }
        public uint AsUInt() { Ensure(DbValueType.UInt); return (uint)_long0; }
        public long AsLong() { Ensure(DbValueType.Long); return _long0; }
        public ulong AsULong() { Ensure(DbValueType.ULong); return unchecked((ulong)_long0); }
        public float AsFloat()
        {
            Ensure(DbValueType.Float);
            var bits = _long0;
            return Unsafe.As<long, float>(ref bits);
        }
        public double AsDouble()
        {
            Ensure(DbValueType.Double);
            var bits = _long0;
            return Unsafe.As<long, double>(ref bits);
        }
        public decimal AsDecimal() { Ensure(DbValueType.Decimal); return _decimal; }
        public DateTime AsDateTimeUtc() { Ensure(DbValueType.DateTimeUTC); return new DateTime(_long0, DateTimeKind.Utc); }
        public string AsString()
        {
            if (_type is DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B or DbValueType.StrVar)
                return (string)_ref!;
            throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {_type}");
        }
        public byte[] AsBytes()
        {
            if (_type is DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar)
                return (byte[])_ref!;
            throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {_type}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Ensure(DbValueType expected)
        {
            if (_type != expected) throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: expected {expected}, got {_type}");
        }

        public static DbCell FromObject(object value)
        {
            return value switch
            {
                bool b => FromBool(b),
                byte b => FromByte(b),
                int i => FromInt(i),
                uint u => FromUInt(u),
                long l => FromLong(l),
                ulong ul => FromULong(ul),
                float f => FromFloat(f),
                double d => FromDouble(d),
                decimal m => FromDecimal(m),
                DateTime dt => FromDateTimeUtc(dt),
                string s => FromString(s),
                byte[] bytes => FromBytes(bytes),
                _ => throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {value?.GetType().Name}"),
            };
        }

        public static DbCell FromObject(object value, DbValueType columnType)
        {
            var cell = FromObject(value);
            return cell.WithColumnType(columnType);
        }

        public DbCell WithColumnType(DbValueType columnType)
        {
            if (_type == columnType) return this;
            // retarget string/bytes fixed vs var without changing payload
            if ((_type is DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B or DbValueType.StrVar)
                && (columnType is DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B or DbValueType.StrVar))
            {
                var c = this;
                c._type = columnType;
                return c;
            }
            if ((_type is DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar)
                && (columnType is DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar))
            {
                var c = this;
                c._type = columnType;
                return c;
            }
            if (_type != columnType && IsCompatibleScalar(_type, columnType))
            {
                var c = this;
                c._type = columnType;
                return c;
            }
            return this;
        }

        private static bool IsCompatibleScalar(DbValueType a, DbValueType b) => a == b;

        public object ToObject()
        {
            return _type switch
            {
                DbValueType.Bool => AsBool(),
                DbValueType.Byte => AsByte(),
                DbValueType.Int => AsInt(),
                DbValueType.UInt => AsUInt(),
                DbValueType.Long => AsLong(),
                DbValueType.ULong => AsULong(),
                DbValueType.Float => AsFloat(),
                DbValueType.Double => AsDouble(),
                DbValueType.Decimal => AsDecimal(),
                DbValueType.DateTimeUTC => AsDateTimeUtc(),
                DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B or DbValueType.StrVar => AsString(),
                DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar => AsBytes(),
                _ => throw LumException.Raise(LumExceptionMessage.UnknownValType),
            };
        }

        public bool MatchesColumn(DbValueType columnType)
        {
            return columnType switch
            {
                DbValueType.Bool => _type == DbValueType.Bool,
                DbValueType.Byte => _type == DbValueType.Byte,
                DbValueType.Int => _type == DbValueType.Int,
                DbValueType.UInt => _type == DbValueType.UInt,
                DbValueType.Long => _type == DbValueType.Long,
                DbValueType.ULong => _type == DbValueType.ULong,
                DbValueType.Float => _type == DbValueType.Float,
                DbValueType.Double => _type == DbValueType.Double,
                DbValueType.Decimal => _type == DbValueType.Decimal,
                DbValueType.DateTimeUTC => _type == DbValueType.DateTimeUTC,
                DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B or DbValueType.StrVar => _ref is string,
                DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar => _ref is byte[],
                _ => false,
            };
        }

        public void EnsureFitsColumn(DbValueType columnType)
        {
            switch (columnType)
            {
                case DbValueType.Str8B:
                case DbValueType.Str16B:
                case DbValueType.Str32B:
                    {
                        var max = columnType.GetLength();
                        var n = Encoding.UTF8.GetByteCount(AsString());
                        if (n > max)
                            throw LumException.Raise($"{LumExceptionMessage.FixedLengthTooLong}: {columnType} needs {n} > {max}");
                        break;
                    }
                case DbValueType.Bytes8:
                case DbValueType.Bytes16:
                case DbValueType.Bytes32:
                    {
                        var max = columnType.GetLength();
                        var n = AsBytes().Length;
                        if (n > max)
                            throw LumException.Raise($"{LumExceptionMessage.FixedLengthTooLong}: {columnType} needs {n} > {max}");
                        break;
                    }
            }
        }

        public Span<byte> Serialize(Span<byte> buffer)
        {
            EnsureFitsColumn(_type);
            switch (_type)
            {
                case DbValueType.Bool:
                    BitConverter.TryWriteBytes(buffer, AsBool());
                    return buffer;
                case DbValueType.Byte:
                    buffer[0] = AsByte();
                    return buffer;
                case DbValueType.Int:
                    BitConverter.TryWriteBytes(buffer, AsInt());
                    return buffer;
                case DbValueType.UInt:
                    BitConverter.TryWriteBytes(buffer, AsUInt());
                    return buffer;
                case DbValueType.Long:
                    BitConverter.TryWriteBytes(buffer, AsLong());
                    return buffer;
                case DbValueType.ULong:
                    BitConverter.TryWriteBytes(buffer, AsULong());
                    return buffer;
                case DbValueType.Float:
                    BitConverter.TryWriteBytes(buffer, AsFloat());
                    return buffer;
                case DbValueType.Double:
                    BitConverter.TryWriteBytes(buffer, AsDouble());
                    return buffer;
                case DbValueType.DateTimeUTC:
                    BitConverter.TryWriteBytes(buffer, _long0);
                    return buffer;
                case DbValueType.Decimal:
                    _decimal.ToSpan(buffer);
                    return buffer;
                case DbValueType.Str8B:
                case DbValueType.Str16B:
                case DbValueType.Str32B:
                case DbValueType.StrVar:
                    buffer.Clear();
                    Encoding.UTF8.GetBytes(AsString(), buffer);
                    return buffer;
                case DbValueType.Bytes8:
                case DbValueType.Bytes16:
                case DbValueType.Bytes32:
                case DbValueType.BytesVar:
                    buffer.Clear();
                    AsBytes().CopyTo(buffer);
                    return buffer;
                default:
                    throw LumException.Raise(LumExceptionMessage.UnknownValType);
            }
        }

        public static DbCell Deserialize(ReadOnlySpan<byte> value, DbValueType type)
        {
            switch (type)
            {
                case DbValueType.Bool: return FromBool(BitConverter.ToBoolean(value));
                case DbValueType.Byte: return FromByte(value[0]);
                case DbValueType.Int: return FromInt(BitConverter.ToInt32(value));
                case DbValueType.UInt: return FromUInt(BitConverter.ToUInt32(value));
                case DbValueType.Float: return FromFloat(BitConverter.ToSingle(value));
                case DbValueType.Long: return FromLong(BitConverter.ToInt64(value));
                case DbValueType.ULong: return FromULong(BitConverter.ToUInt64(value));
                case DbValueType.Double: return FromDouble(BitConverter.ToDouble(value));
                case DbValueType.DateTimeUTC: return new DbCell { _type = DbValueType.DateTimeUTC, _long0 = BitConverter.ToInt64(value) };
                case DbValueType.Decimal:
                    {
                        Span<byte> tmp = stackalloc byte[16];
                        value.Slice(0, 16).CopyTo(tmp);
                        return FromDecimal(tmp.ToDecimal());
                    }
                case DbValueType.Str8B:
                case DbValueType.Str16B:
                case DbValueType.Str32B:
                    return new DbCell { _type = type, _ref = Encoding.UTF8.GetString(value).TrimEnd('\0') };
                case DbValueType.Bytes8:
                case DbValueType.Bytes16:
                case DbValueType.Bytes32:
                    return new DbCell { _type = type, _ref = value.ToArray() };
                default:
                    throw LumException.Raise(LumExceptionMessage.UnknownValType);
            }
        }

        internal static DbCell Deserialize(Span<byte> value, DbCache db, DbValueType type)
        {
            if (type == DbValueType.StrVar)
            {
                NodeLink.Create(value, out var link);
                var outBytes = DataVarManager.GetDataVar(db, link);
                return new DbCell { _type = type, _ref = Encoding.UTF8.GetString(outBytes).TrimEnd('\0') };
            }
            if (type == DbValueType.BytesVar)
            {
                NodeLink.Create(value, out var link);
                return new DbCell { _type = type, _ref = DataVarManager.GetDataVar(db, link) };
            }
            return Deserialize((ReadOnlySpan<byte>)value, type);
        }
    }
}
