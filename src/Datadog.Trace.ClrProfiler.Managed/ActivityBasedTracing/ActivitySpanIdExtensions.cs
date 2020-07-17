using System;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler
{
    internal static class ActivitySpanIdExtensions
    {
        private const int SpanIdByteLength = 8;

        public static ulong ToUInt64(this ActivitySpanId rootSpanId)
        {
            string hexStr = rootSpanId.ToHexString();

            if (hexStr.Length != SpanIdByteLength * 2)
            {
                // This is is a static check. It should really never happen.
                throw new Exception($"There is a wrong assumption about the {nameof(ActivitySpanId)} Hex String representation format in {typeof(ActivitySpanIdExtensions).FullName}.");
            }

            ulong spanValue = 0;

            char c1, c2;
            for (int b = 0; b < SpanIdByteLength; b++)
            {
                c1 = hexStr[(b << 1)];
                c2 = hexStr[(b << 1) + 1];

                spanValue <<= 4;
                spanValue |= HexCharToUInt64(c1);
                spanValue <<= 4;
                spanValue |= HexCharToUInt64(c2);
            }

            return spanValue;
        }

        private static ulong HexCharToUInt64(char hexChar)
        {
            switch (hexChar)
            {
                case '0': return 0;
                case '1': return 1;
                case '2': return 2;
                case '3': return 3;
                case '4': return 4;
                case '5': return 5;
                case '6': return 6;
                case '7': return 7;
                case '8': return 8;
                case '9': return 9;
                case 'a':
                case 'A': return 10;
                case 'b':
                case 'B': return 11;
                case 'c':
                case 'C': return 12;
                case 'd':
                case 'D': return 13;
                case 'e':
                case 'E': return 14;
                case 'f':
                case 'F': return 15;
                default:
                    throw new ArgumentOutOfRangeException(paramName: nameof(hexChar), message: $"Specified character ('{hexChar}') is not a valid hex character.");
            }
        }
    }
}
