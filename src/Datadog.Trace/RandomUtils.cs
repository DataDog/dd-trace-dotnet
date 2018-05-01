using System;

namespace Datadog.Trace
{
    internal static class RandomUtils
    {
        public static ulong NextUInt63(this Random rnd)
        {
            // From https://stackoverflow.com/a/677390
            var buffer = new byte[sizeof(ulong)];
            rnd.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0) & (~(1 << 63));
        }
    }
}
