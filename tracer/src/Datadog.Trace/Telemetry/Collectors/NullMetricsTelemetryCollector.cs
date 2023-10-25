// <copyright file="NullMetricsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Telemetry;

internal partial class NullMetricsTelemetryCollector : IMetricsTelemetryCollector
{
    public static readonly NullMetricsTelemetryCollector Instance = new();

    public void Record(PublicApiUsage api)
    {
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public MetricResults GetMetrics() => new(null, null);

    public void SetWafVersion(string wafVersion)
    {
    }
}
