using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Specific;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Utils.ByteUtils;
using System.Runtime.CompilerServices;
using System.Text;

namespace LumDbEngine.Element.Structure
{
    public enum DbValueType : byte
    {
        Unknow = 0,
        Bool = 1,
        Int = 2,
        UInt = 3,
        Long = 4,
        ULong = 5,
        Float = 6,
        Double = 7,
        Byte = 8,
        DateTimeUTC = 9,
        Decimal = 10,
        Str8B = 20,
        Str16B = 22,
        Str32B = 24,
        Bytes8 = 40,
        Bytes16 = 42,
        Bytes32 = 44,

        // ----------- split -----------
        // Splitter     = 100
        // ----------- split -----------

        StrVar = 101,
        BytesVar = 102,
    }

    internal static class DbValueTypeUtils
    {
        public const byte DataVarSplitter = 100;

        public static bool CheckType(this DbValueType type, object value)
        {
            return type switch
            {
                DbValueType.Unknow => false,
                DbValueType.Bool => value is bool,
                DbValueType.Byte => value is byte,
                DbValueType.Int => value is int,
                DbValueType.UInt => value is uint,
                DbValueType.Float => value is float,
                DbValueType.Long => value is long,
                DbValueType.ULong => value is ulong,
                DbValueType.Double => value is double,
                DbValueType.DateTimeUTC => value is DateTime dt && dt.Kind == DateTimeKind.Utc,
                DbValueType.Decimal => value is decimal,
                DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B or DbValueType.StrVar => value is string,
                DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar => value is IList<byte>,
                _ => throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {type}"),
            };
        }

        /// <summary>
        /// Check if the data type of a length less than 32 bytes.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsValidFix32(this DbValueType type)
        {
            return (byte)type <= DataVarSplitter;
        }

        internal static object DeserializeBytesToValue(this Span<byte> value, DbCache db, DbValueType type)
        {
            if (type == DbValueType.StrVar)
            {
                NodeLink.Create(value, out var link);
                var outBytes = DataVarManager.GetDataVar(db, link);
                return Encoding.UTF8.GetString(outBytes).TrimEnd('\0');
            }
            else if (type == DbValueType.BytesVar)
            {
                NodeLink.Create(value, out var link);
                return DataVarManager.GetDataVar(db, link);
            }
            else
            {
                return DeserializeBytesToObject(value, type);
            }
        }

        public static object DeserializeBytesToObject(this Span<byte> value, DbValueType type)
        {
            return type switch
            {
                DbValueType.Bool => BitConverter.ToBoolean(value),
                DbValueType.Byte => value[0],
                DbValueType.Int => BitConverter.ToInt32(value),
                DbValueType.UInt => BitConverter.ToUInt32(value),
                DbValueType.Float => BitConverter.ToSingle(value),
                DbValueType.Long => BitConverter.ToInt64(value),
                DbValueType.ULong => BitConverter.ToUInt64(value),
                DbValueType.Double => BitConverter.ToDouble(value),
                DbValueType.DateTimeUTC => DateTime.FromBinary(BitConverter.ToInt64(value)),
                DbValueType.Decimal => value.ToDecimal(),
                DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B => Encoding.UTF8.GetString(value).TrimEnd('\0'),
                DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar or DbValueType.StrVar => value.ToArray(),
                _ => throw LumException.Raise(LumExceptionMessage.UnknownValType),
            };
        }

        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        //public static byte[] SerializeObjectToBytes(this object value)
        //{
        //    return value switch
        //    {
        //        bool => BitConverter.GetBytes((bool)value),
        //        int => BitConverter.GetBytes((int)value),
        //        uint => BitConverter.GetBytes((uint)value),
        //        long => BitConverter.GetBytes((long)value),
        //        ulong => BitConverter.GetBytes((ulong)value),
        //        float => BitConverter.GetBytes((float)value),
        //        double => BitConverter.GetBytes((double)value),
        //        byte => [(byte)value],
        //        DateTime => ((DateTime)value).Kind == DateTimeKind.Utc ? BitConverter.GetBytes(((DateTime)value).Ticks) : throw LumException.Raise("dateTime must be utc kind."),
        //        decimal =>((decimal)value).ToBytes(),
        //        string => Encoding.UTF8.GetBytes((string)value),
        //        byte[] => (byte[])value,
        //        _ => throw LumException.Raise("Unknown value type")
        //    };
        //}

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static Span<byte> SerializeObjectToBytes(this object value, Span<byte> buffer)
        {
            try
            {
                switch (value)
                {
                    case bool:
                        if (BitConverter.TryWriteBytes(buffer, (bool)value))
                        {
                            return buffer;
                        }
                        else
                        {
                            return [];
                        }
                    case int:
                        if (BitConverter.TryWriteBytes(buffer, (int)value))
                        {
                            return buffer;
                        }
                        else
                        {
                            return [];
                        }
                    case uint:
                        if (BitConverter.TryWriteBytes(buffer, (uint)value))
                        {
                            return buffer;
                        }
                        else
                        {
                            goto default;
                        }
                    case long:
                        if (BitConverter.TryWriteBytes(buffer, (long)value))
                        {
                            return buffer;
                        }
                        else
                        {
                            goto default;
                        }
                    case ulong:
                        if (BitConverter.TryWriteBytes(buffer, (ulong)value))
                        {
                            return buffer;
                        }
                        else
                        {
                            goto default;
                        };
                    case float:
                        if (BitConverter.TryWriteBytes(buffer, (float)value))
                        {
                            return buffer;
                        }
                        else
                        {
                            goto default;
                        }
                    case double:
                        if (BitConverter.TryWriteBytes(buffer, (double)value))
                        {
                            return buffer;
                        }
                        else
                        {
                            goto default;
                        }
                    case byte:
                        {
                            buffer[0] = (byte)value;
                            return buffer;
                        }
                    case DateTime:
                        if (((DateTime)value).Kind != DateTimeKind.Utc)
                        {
                            throw LumException.Raise(LumExceptionMessage.DateTimeUtcError);
                        }

                        if (BitConverter.TryWriteBytes(buffer, ((DateTime)value).Ticks))
                        {
                            return buffer;
                        }
                        else
                        {
                            goto default;
                        }
                    case decimal:
                        ((decimal)value).ToSpan(buffer);
                        return buffer;

                    case string:
                        Encoding.UTF8.GetBytes((string)value, buffer);
                        return buffer;

                    case byte[]:
                        ((byte[])value).CopyTo(buffer);
                        return buffer;

                    default:
                        throw LumException.Raise(LumExceptionMessage.UnknownValType);
                };
            }
            catch (Exception ex)
            {
                throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {ex.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetLength(this DbValueType type)
        {
            return type switch
            {
                DbValueType.Bool or DbValueType.Byte => 1,
                DbValueType.Int or DbValueType.UInt or DbValueType.Float => 4,
                DbValueType.Long or DbValueType.ULong or DbValueType.Double or DbValueType.DateTimeUTC or DbValueType.Str8B or DbValueType.Bytes8 => 8,
                DbValueType.Str16B or DbValueType.Bytes16 or DbValueType.Decimal => 16,
                DbValueType.Bytes32 or DbValueType.Str32B => 32,
                DbValueType.BytesVar or DbValueType.StrVar => NodeLink.Size,// the size of NodeLink
                _ => throw LumException.Raise(LumExceptionMessage.UnknownValType),
            };
        }
    }
}