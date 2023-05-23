// <copyright file="TelemetryMetrics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading;

namespace Datadog.Trace.Telemetry.Metrics;

internal static class TelemetryMetrics
{
    private static IMetricsTelemetryCollector _instance = NullMetricsTelemetryCollector.Instance;

    /// <summary>
    /// Gets the static instance used to record telemetry
    /// </summary>
    public static IMetricsTelemetryCollector Instance => Volatile.Read(ref _instance);

    public static void Disable()
    {
        Interlocked.Exchange(ref _instance, NullMetricsTelemetryCollector.Instance);
    }
}
