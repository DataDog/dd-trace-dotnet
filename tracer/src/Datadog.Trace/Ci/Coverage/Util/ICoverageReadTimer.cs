// <copyright file="ICoverageReadTimer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage.Util;

internal interface ICoverageReadTimer
{
    /// <summary>Gets monotonic time elapsed since the timer was created.</summary>
    TimeSpan Elapsed { get; }

    void Sleep(TimeSpan delay);
}

internal sealed class CoverageReadTimer : ICoverageReadTimer
{
    private readonly long _started = Stopwatch.GetTimestamp();

    public TimeSpan Elapsed => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _started);

    public void Sleep(TimeSpan delay) => Thread.Sleep(delay);
}
