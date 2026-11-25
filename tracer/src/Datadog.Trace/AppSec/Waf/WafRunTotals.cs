// <copyright file="WafRunTotals.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Util;

namespace Datadog.Trace.AppSec.Waf;

/// <summary>
/// The durations of the WAF runs of one scope (WAF or RASP) of a request. They are kept in their source units
/// and only converted to microseconds over the total, so that the sub-microsecond part of each run is not lost.
/// </summary>
internal struct WafRunTotals
{
    private ulong _durationNanos;
    private long _durationWithBindingsTicks;

    /// <summary>
    /// Gets the duration of the runs as reported by the WAF, in microseconds
    /// </summary>
    public readonly ulong DurationMicroseconds => _durationNanos / 1000;

    /// <summary>
    /// Gets the wall-clock duration of the runs, including the bindings, in microseconds
    /// </summary>
    public readonly ulong DurationWithBindingsMicroseconds => (ulong)(StopwatchHelpers.GetElapsedMilliseconds(_durationWithBindingsTicks) * 1000);

    public void AddDuration(ulong nanos) => _durationNanos += nanos;

    public void AddDurationWithBindings(long stopwatchTicks) => _durationWithBindingsTicks += stopwatchTicks;
}
