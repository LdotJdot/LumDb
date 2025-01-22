using System.Runtime.CompilerServices;

namespace LumDbEngine.Element.Exceptions
{
    public class LumException : Exception
    {
        private LumException(string? message) : base(message)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNotEqual<T>(T value1, T value2, string? message)
        {
            if (value1 == null || value2 == null || !value1.Equals(value2))
            {
                throw new LumException(message);
            }            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNotTrue(bool value, string? message)
        {
            if (!value)
            {
                throw new LumException(message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNull<T>(T? value, string? message) where T : class
        {
            if (value == null)
            {
                throw new LumException(message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNullOrEmptyBytes(byte[] value, string? message)
        {
            if (value == null || value.Length == 0)
            {
                throw new LumException(message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfTrue(bool value, string? message)
        {
            if (value)
            {
                throw new LumException(message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LumException Raise(string? message)
        {
            return new LumException(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LumException Throw(string? message)
        {
            throw new LumException(message);
        }
    }
}