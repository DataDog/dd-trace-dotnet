// <copyright file="Gauge.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Telemetry.Metrics;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line", Justification = "It's easier to read")]
[TelemetryMetricType(TelemetryMetricType.Gauge)]
internal enum Gauge
{
    /// <summary>
    /// The number of unique buckets created for stats aggregation
    /// </summary>
    [TelemetryMetric("stats_buckets")] StatsBuckets,

    /// <summary>
    /// The number of profiler instrumentations created, tagged by the component (TraceAnnotations, DD_TRACE_METHODS, Calltarget, CallTargetDerived etc)
    /// </summary>
    [TelemetryMetric<MetricTags.InstrumentationComponent>("instrumentations", isCommon: false)] Instrumentations,

    /// <summary>
    /// The number of logs currently enqueued to the direct log submission sink
    /// </summary>
    [TelemetryMetric("direct_log_queue.length", isCommon: false)] DirectLogQueue,
}
