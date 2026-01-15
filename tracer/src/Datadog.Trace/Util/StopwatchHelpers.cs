// <copyright file="StopwatchHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util
{
    internal static class StopwatchHelpers
    {
        private static readonly double DateTimeTickFrequency = 10000000.0 / Stopwatch.Frequency;
        private static readonly double TimestampToMilliseconds = 1000.0 / Stopwatch.Frequency;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan GetElapsed(long stopwatchTicks)
            => new(GetElapsedTicks(stopwatchTicks));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetElapsedTicks(long stopwatchTicks)
            => (long)(stopwatchTicks * DateTimeTickFrequency);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetElapsedMilliseconds(long stopwatchTicks)
            => stopwatchTicks * TimestampToMilliseconds;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetElapsedMilliseconds(this Stopwatch sw)
            => sw.ElapsedTicks * TimestampToMilliseconds;
    }
}
