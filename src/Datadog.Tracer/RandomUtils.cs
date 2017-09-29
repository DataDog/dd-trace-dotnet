using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Tracer
{
    public static class RandomUtils
    {
        public static UInt64 NextUInt64(this Random rnd)
        {
            // From https://stackoverflow.com/a/677390
            var buffer = new byte[sizeof(UInt64)];
            rnd.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }
    }
}
