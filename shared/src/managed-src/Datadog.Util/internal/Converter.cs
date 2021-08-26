using System;
using System.Runtime.CompilerServices;

namespace Datadog.Util
{
    /// <summary>
    /// It would be great to call this type <c>Convert</c>, but that name is already taken by the framework. :)
    /// </summary>
    internal static class Converter
    {
        public static class UnixTimeSeconds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DateTimeOffset ToDateTimeOffset(long unixTimeUtcSeconds)
            {
#if NET45
                return ToDateTimeOffsetImplementation(unixTimeUtcSeconds);
#elif NET451
                return ToDateTimeOffsetImplementation(unixTimeUtcSeconds);
#elif NET452
                return ToDateTimeOffsetImplementation(unixTimeUtcSeconds);
#else
                return DateTimeOffset.FromUnixTimeSeconds(unixTimeUtcSeconds);
#endif
            }

#pragma warning disable IDE0079  // Remove unnecessary suppression, as it is necessary for some targets
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051: Remove unused private member", Justification = "Used in some, but not all compilation targets")]
#pragma warning restore IDE0079  // Remove unnecessary suppression
            private static DateTimeOffset ToDateTimeOffsetImplementation(long unixTimeUtcSeconds)
            {
                // Precomputed:
                const int DaysTo1970 = 719162;
                const long UnixEpochTicks = TimeSpan.TicksPerDay * DaysTo1970;  // 621,355,968,000,000,000
                const long MinSeconds = -62135596800;
                const long MaxSeconds = 253402300799;

                if (unixTimeUtcSeconds < MinSeconds || unixTimeUtcSeconds > MaxSeconds)
                {
                    throw new ArgumentOutOfRangeException(nameof(unixTimeUtcSeconds),
                                                          $"Valid values are between {MinSeconds} and {MaxSeconds} seconds, inclusive.");
                }

                long ticks = unixTimeUtcSeconds * TimeSpan.TicksPerSecond + UnixEpochTicks;
                return new DateTimeOffset(ticks, TimeSpan.Zero);
            }
        }
    }
}
