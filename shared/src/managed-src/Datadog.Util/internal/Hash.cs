using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Datadog.Util
{
    internal static class Hash
    {
        private const int NullObjectHash = 0;

        public static uint ComputeFastStable(string str)
        {
            if (str == null)
            {
                return NullObjectHash;
            }

            uint hash = 17;
            int strLen = str.Length;
            uint currChar;

            unchecked
            {
                for (int p = 0; p < strLen; p++)
                {
                    currChar = (uint)str[p];
                    hash = (hash << 5) - hash + currChar;   // 31 * hash + currChar
                }
            }

            return hash;
        }

        public static ulong ComputeFastStableUInt64(string str)
        {
            if (str == null)
            {
                return NullObjectHash;
            }

            ulong hash = 17;
            int strLen = str.Length;
            ulong currChar;

            unchecked
            {
                for (int p = 0; p < strLen; p++)
                {
                    currChar = (ulong)str[p];
                    hash = (hash << 5) - hash + currChar;   // 31 * hash + currChar
                }
            }

            return hash;
        }

        public static uint ComputeFastStable(Guid guid)
        {
            byte[] guidBytes = guid.ToByteArray();
            uint hash = 17;
            uint currByte;

            unchecked
            {
                for (int p = 0; p < guidBytes.Length; p++)
                {
                    currByte = (uint)guidBytes[p];
                    hash = (hash << 5) - hash + currByte;   // 31 * hash + currByte
                }
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int hash1)
        {
            return hash1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Combine(uint hash1)
        {
            return hash1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ulong hash1)
        {
            return hash1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int hash1, int hash2)
        {
            int hash = 17;
            unchecked
            {
                hash = (hash * 23) + hash1;
                hash = (hash * 23) + hash2;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Combine(uint hash1, uint hash2)
        {
            uint hash = 17;
            unchecked
            {
                hash = (hash * 23) + hash1;
                hash = (hash * 23) + hash2;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ulong hash1, ulong hash2)
        {
            ulong hash = 17;
            unchecked
            {
                hash = (hash * 23) + hash1;
                hash = (hash * 23) + hash2;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int hash1, int hash2, int hash3)
        {
            int hash = 17;
            unchecked
            {
                hash = (hash * 23) + hash1;
                hash = (hash * 23) + hash2;
                hash = (hash * 23) + hash3;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Combine(uint hash1, uint hash2, uint hash3)
        {
            uint hash = 17;
            unchecked
            {
                hash = (hash * 23) + hash1;
                hash = (hash * 23) + hash2;
                hash = (hash * 23) + hash3;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ulong hash1, ulong hash2, ulong hash3)
        {
            ulong hash = 17;
            unchecked
            {
                hash = (hash * 23) + hash1;
                hash = (hash * 23) + hash2;
                hash = (hash * 23) + hash3;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int hash1, int hash2, int hash3, int hash4)
        {
            int hash = 17;
            unchecked
            {
                hash = (hash * 23) + hash1;
                hash = (hash * 23) + hash2;
                hash = (hash * 23) + hash3;
                hash = (hash * 23) + hash4;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Combine(uint hash1, uint hash2, uint hash3, uint hash4)
        {
            uint hash = 17;
            unchecked
            {
                hash = (hash * 23) + hash1;
                hash = (hash * 23) + hash2;
                hash = (hash * 23) + hash3;
                hash = (hash * 23) + hash4;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ulong hash1, ulong hash2, ulong hash3, ulong hash4)
        {
            ulong hash = 17;
            unchecked
            {
                hash = (hash * 23) + hash1;
                hash = (hash * 23) + hash2;
                hash = (hash * 23) + hash3;
                hash = (hash * 23) + hash4;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(IEnumerable<int> arr)
        {
            if (arr == null)
            {
                return NullObjectHash;
            }

            int hash = 17;
            unchecked
            {
                foreach (int n in arr)
                {
                    hash = (hash * 23) + n;
                }
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Combine(uint[] arr)
        {
            if (arr == null)
            {
                return NullObjectHash;
            }

            uint hash = 17;
            unchecked
            {
                foreach (uint n in arr)
                {
                    hash = (hash * 23) + n;
                }
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ulong[] arr)
        {
            if (arr == null)
            {
                return NullObjectHash;
            }

            ulong hash = 17;
            unchecked
            {
                foreach (ulong n in arr)
                {
                    hash = (hash * 23) + n;
                }
            }

            return hash;
        }
    }
}
