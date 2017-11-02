using System;

namespace Datadog.Trace
{
    static class TimeUtils
    {
        private const long NanoSecondsPerTick = 1000000 / TimeSpan.TicksPerMillisecond;
        private static readonly long UnixEpochInTicks = new DateTimeOffset(1970, 1, 1,  0, 0, 0, TimeSpan.Zero).Ticks;

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
