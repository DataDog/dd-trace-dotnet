using System;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class RandomExtensions
    {
        public static ulong NextUInt63(this Random rnd)
        {
            // From https://stackoverflow.com/a/677390
            var buffer = new byte[sizeof(ulong)];
            rnd.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0) & 0x7FFFFFFFFFFFFFFF;
        }
    }
}
