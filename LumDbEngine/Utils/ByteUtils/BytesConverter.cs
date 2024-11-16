using LumDbEngine.Element.Exceptions;

namespace LumDbEngine.Utils.ByteUtils
{
    internal static class BytesConverter
    {
        public static decimal ToDecimal(this Span<byte> bytes)
        {
            if (bytes.Length != 16)
                throw LumException.Raise("decimal should be 16 byte length");

            // 将字节数组拆分为4个int32部分
            int[] bits = new int[4];
            for (int i = 0; i < 4; i++)
            {
                bits[i] = BitConverter.ToInt32(bytes.Slice(i * 4, 4));
            }

            // 将4个int32部分组合成一个decimal
            return new decimal(bits);
        }

        public static unsafe void ToSpan(this decimal value, Span<byte> bytes)
        {
            // 将decimal拆分为4个int32部分
            int[] bits = decimal.GetBits(value);

            // 将每个int32部分转换为字节并存储在数组中
            for (int i = 0; i < 4; i++)
            {
#pragma warning disable CA2014
                Span<byte> buffer = stackalloc byte[4];
                BitConverter.TryWriteBytes(buffer, bits[i]);
                buffer.CopyTo(bytes.Slice(i * 4, 4));
            }
        }
    }
}