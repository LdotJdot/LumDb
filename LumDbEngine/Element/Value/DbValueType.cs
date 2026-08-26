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

        public static bool CheckType(this DbValueType type, in DbCell value)
        {
            return value.MatchesColumn(type);
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
            return DbCell.Deserialize(value, db, type).ToObject();
        }

        public static object DeserializeBytesToObject(this Span<byte> value, DbValueType type)
        {
            return DbCell.Deserialize(value, type).ToObject();
        }

        public static DbCell DeserializeBytesToCell(this Span<byte> value, DbValueType type)
        {
            return DbCell.Deserialize(value, type);
        }

        public static DbCell DeserializeBytesToCell(this Span<byte> value, DbCache db, DbValueType type)
        {
            return DbCell.Deserialize(value, db, type);
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
                return DbCell.FromObject(value).Serialize(buffer);
            }
            catch (Exception ex)
            {
                throw LumException.Raise($"{LumExceptionMessage.UnknownValType}: {ex.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> SerializeCellToBytes(this in DbCell cell, Span<byte> buffer)
        {
            return cell.Serialize(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32(uint value, Span<byte> buffer)
        {
            BitConverter.TryWriteBytes(buffer, value);
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