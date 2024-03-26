// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Telemetry;

/// <summary>
/// A stub just there to satisfy imported links
/// </summary>
internal static class TelemetryFactory
{
    public static StubMetricCollector Metrics { get; } = new();

    public static IConfigurationTelemetry Config => NullConfigurationTelemetry.Instance;

    internal class StubMetricCollector
    {
        public void RecordCountLogCreated(MetricTags.LogLevel logLevel)
        {
        }

        public void Record(PublicApiUsage publicApiUsage)
        {
        }
    }
}
