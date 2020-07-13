using System;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class RandomExtensions
    {
        public static ulong NextUInt63(this Random rnd)
        {
            long high = rnd.Next(int.MinValue, int.MaxValue);
            long low = rnd.Next(int.MinValue, int.MaxValue);

            // Concatenate both values, and truncate the 32 top bits from low
            var value = high << 32 | (low & 0xFFFFFFFF);

            return (ulong)value & 0x7FFFFFFFFFFFFFFF;
        }
    }
}
