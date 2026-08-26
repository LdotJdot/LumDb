using System.Globalization;
using LumDbEngine.Element.Structure;

namespace LumDbExplorer
{
    internal static class CellCodec
    {
        public static string Format(in DbCell cell)
        {
            try
            {
                return cell.Type switch
                {
                    DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar
                        => Convert.ToHexString(cell.AsBytes()),
                    DbValueType.DateTimeUTC => cell.AsDateTimeUtc().ToString("o", CultureInfo.InvariantCulture),
                    DbValueType.Bool => cell.AsBool() ? "true" : "false",
                    DbValueType.Float => cell.AsFloat().ToString("G9", CultureInfo.InvariantCulture),
                    DbValueType.Double => cell.AsDouble().ToString("G17", CultureInfo.InvariantCulture),
                    DbValueType.Decimal => cell.AsDecimal().ToString(CultureInfo.InvariantCulture),
                    _ => Convert.ToString(cell.ToObject(), CultureInfo.InvariantCulture) ?? "",
                };
            }
            catch
            {
                return "";
            }
        }

        public static DbCell Parse(string text, DbValueType type)
        {
            text ??= "";
            return type switch
            {
                DbValueType.Bool => DbCell.FromBool(text is "1" or "true" or "True" or "TRUE" or "yes"),
                DbValueType.Byte => DbCell.FromByte(byte.Parse(text, CultureInfo.InvariantCulture)),
                DbValueType.Int => DbCell.FromInt(int.Parse(text, CultureInfo.InvariantCulture)),
                DbValueType.UInt => DbCell.FromUInt(uint.Parse(text, CultureInfo.InvariantCulture)),
                DbValueType.Long => DbCell.FromLong(long.Parse(text, CultureInfo.InvariantCulture)),
                DbValueType.ULong => DbCell.FromULong(ulong.Parse(text, CultureInfo.InvariantCulture)),
                DbValueType.Float => DbCell.FromFloat(float.Parse(text, CultureInfo.InvariantCulture)),
                DbValueType.Double => DbCell.FromDouble(double.Parse(text, CultureInfo.InvariantCulture)),
                DbValueType.Decimal => DbCell.FromDecimal(decimal.Parse(text, CultureInfo.InvariantCulture)),
                DbValueType.DateTimeUTC => DbCell.FromDateTimeUtc(ParseUtc(text)),
                DbValueType.Str8B or DbValueType.Str16B or DbValueType.Str32B or DbValueType.StrVar
                    => DbCell.FromString(text, type),
                DbValueType.Bytes8 or DbValueType.Bytes16 or DbValueType.Bytes32 or DbValueType.BytesVar
                    => DbCell.FromBytes(ParseBytes(text), type),
                _ => throw new FormatException($"不支持的列类型 {type}"),
            };
        }

        public static bool TryParseType(string name, out DbValueType type)
            => Enum.TryParse(name, ignoreCase: true, out type) && type != DbValueType.Unknow;

        static DateTime ParseUtc(string text)
        {
            var dt = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            };
        }

        static byte[] ParseBytes(string text)
        {
            var hex = text.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
            if (hex.Length == 0) return [];
            return Convert.FromHexString(hex);
        }
    }
}
