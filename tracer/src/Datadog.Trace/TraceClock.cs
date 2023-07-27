// <copyright file="TraceClock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util;

namespace Datadog.Trace;

internal readonly struct TraceClock
{
    private readonly DateTimeOffset _utcStart;
    private readonly long _timestamp;

    public TraceClock()
    {
        _utcStart = DateTimeOffset.UtcNow;
        _timestamp = Stopwatch.GetTimestamp();
    }

    public DateTimeOffset UtcNow
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _utcStart.Add(Elapsed);
    }

    private TimeSpan Elapsed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan ElapsedSince(DateTimeOffset date) => UtcNow - date;
}
