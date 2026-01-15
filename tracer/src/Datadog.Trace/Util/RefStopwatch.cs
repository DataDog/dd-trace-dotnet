// <copyright file="RefStopwatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

internal ref struct RefStopwatch
{
    private long _started;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RefStopwatch()
    {
        _started = Stopwatch.GetTimestamp();
    }

    public TimeSpan Elapsed => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _started);

    public double ElapsedMilliseconds => StopwatchHelpers.GetElapsedMilliseconds(Stopwatch.GetTimestamp() - _started);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RefStopwatch Create() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Restart()
    {
        _started = Stopwatch.GetTimestamp();
    }
}
