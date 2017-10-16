using System;

namespace Datadog.Tracer
{
    static class TimeUtils
    {
        private const long NanoSecondsPerTick = 1000000 / TimeSpan.TicksPerMillisecond;
        private static readonly long UnixEpochInTicks = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks;

        public static long ToUnixTimeNanoseconds(this DateTimeOffset dateTimeOffset)
        {
            return (dateTimeOffset.Ticks - UnixEpochInTicks) * NanoSecondsPerTick;
        }

        public static long ToNanoseconds(this TimeSpan ts)
        {
            return ts.Ticks * NanoSecondsPerTick;
        }
    }
}
