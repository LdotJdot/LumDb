using LumDbEngine.Element.Structure;

namespace LumDbEngine.Element.Value
{
    internal static class ValueConvert
    {
        public static TableValue[] ToTableValues((string columnName, DbCell value)[] values)
        {
            var result = new TableValue[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = (values[i].columnName, values[i].value);
            }
            return result;
        }
    }
}
