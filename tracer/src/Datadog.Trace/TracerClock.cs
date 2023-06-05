// <copyright file="TracerClock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Datadog.Trace.Util;

namespace Datadog.Trace;

internal class TracerClock
{
    private static TracerClock _instance;

    private readonly DateTimeOffset _utcStart = DateTimeOffset.UtcNow;
    private readonly long _timestamp = Stopwatch.GetTimestamp();

    static TracerClock()
    {
        _instance = new TracerClock();
        _ = UpdateClockAsync();
    }

    public static TracerClock Instance => _instance;

    public DateTimeOffset UtcNow => _utcStart.Add(Elapsed);

    private TimeSpan Elapsed => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp);

    public TimeSpan ElapsedSince(DateTimeOffset date)
    {
        return Elapsed + (_utcStart - date);
    }

    private static async Task UpdateClockAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
            _instance = new TracerClock();
        }
    }
}
