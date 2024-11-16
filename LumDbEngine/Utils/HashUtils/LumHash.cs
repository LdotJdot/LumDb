using System.Runtime.CompilerServices;

namespace LumDbEngine.Utils.HashUtils
{
    internal readonly struct LumHash
    {
        public const int HASH_BYTES_SIZE = 16;

        public readonly ulong HashValue;

        public override string ToString()
        {
            return HashValue.ToString();
        }

        internal int Compare(in LumHash other)
        {
            if (HashValue > other.HashValue)
            {
                return 1;
            }
            else if (HashValue < other.HashValue)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int Compare(ulong other)
        {
            if (HashValue > other)
            {
                return 1;
            }
            else if (HashValue < other)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        internal static LumHash Create(Span<byte> bytes)
        {
            return new LumHash(Hash2x64(bytes));
        }

        private LumHash(ulong hashValue)
        {
            this.HashValue = hashValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Hash2x64(Span<byte> bytes)
        {
#if DEBUG
            // bytes = [1, 2, 3];  // simulate hash collision
#endif
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    return MurmurHash2x64A(ptr, bytes.Length);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe ulong MurmurHash2x64A(byte* bString, int len, uint seed = 0)
        {
            // Copyright (c) Microsoft Corporation.
            // Licensed under the MIT license.
            // Garnet.Common

            ulong m = (ulong)0xc6a4a7935bd1e995;
            int r = 47;
            ulong h = seed ^ ((ulong)len * m);
            byte* data = bString;
            byte* end = data + (len - (len & 7));

            while (data != end)
            {
                ulong k;
                k = (ulong)data[0];
                k |= (ulong)data[1] << 8;
                k |= (ulong)data[2] << 16;
                k |= (ulong)data[3] << 24;
                k |= (ulong)data[4] << 32;
                k |= (ulong)data[5] << 40;
                k |= (ulong)data[6] << 48;
                k |= (ulong)data[7] << 56;

                k *= m;
                k ^= k >> r;
                k *= m;
                h ^= k;
                h *= m;

                data += 8;
            }

            int cs = len & 7;

            if (cs >= 7)
                h ^= ((ulong)data[6] << 48);

            if (cs >= 6)
                h ^= ((ulong)data[5] << 40);

            if (cs >= 5)
                h ^= ((ulong)data[4] << 32);

            if (cs >= 4)
                h ^= ((ulong)data[3] << 24);

            if (cs >= 3)
                h ^= ((ulong)data[2] << 16);

            if (cs >= 2) h ^= ((ulong)data[1] << 8);
            if (cs >= 1)
            {
                h ^= (ulong)data[0];
                h *= m;
            }

            h ^= h >> r;
            h *= m;
            h ^= h >> r;
            return h;
        }
    }
}