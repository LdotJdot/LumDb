using LumDbEngine.Element.Exceptions;
using System.Runtime.CompilerServices;
using System.Text;

namespace LumDbEngine.Utils.ByteUtils
{
    internal static class BytesNormalize
    {
        internal static Span<byte> PaddingToBytes(this string str, Span<byte> bytes)
        {
            LumException.ThrowIfTrue(string.IsNullOrWhiteSpace(str), $"length is null or whiteSpace");
            var strByte = Encoding.UTF8.GetBytes(str, bytes);
            return bytes;
        }

        internal static void PaddingToBytes(this Span<byte> bytes, Span<byte> paddedArray, int length)
        {
            LumException.ThrowIfTrue(bytes == null || bytes.Length == 0, $"bytes length is null or whiteSpace");
            LumException.ThrowIfTrue(bytes!.Length > length, $"length is too long (> {length} bytes)");
            Padding(paddedArray, bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Padding(Span<byte> paddedArray, Span<byte> target)
        {
            target.CopyTo(paddedArray);
        }
    }
}