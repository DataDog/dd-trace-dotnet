// <copyright file="DistributionShared.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;
using NS = Datadog.Trace.Telemetry.MetricNamespaceConstants;

namespace Datadog.Trace.Telemetry.Metrics;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line", Justification = "It's easier to read")]
[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = "It's easier to read")]
[TelemetryMetricType(TelemetryMetricType.Distribution, isCiVisibilityMetric: true, isApmMetric: true)]
internal enum DistributionShared
{
#region General Namespace

    /// <summary>
    /// The time it takes to import/initialize the tracer on startup. If this consists of multiple steps/components, tagged by the component/total e.g. `component:call_target`. Component tags will vary by language
    /// </summary>
    [TelemetryMetric<MetricTags.InitializationComponent>("init_time", isCommon: true, MetricNamespaceConstants.General)] InitTime,
#endregion
#region Live Debugger Namespace

    /// <summary>
    /// Memory usage percentage recorded when Dynamic Instrumentation memory pressure changes state.
    /// </summary>
    [TelemetryMetric<MetricTags.DebuggerMemoryPressureState>("memory_pressure.memory_usage_pct", isCommon: true, NS.LiveDebugger)] DebuggerMemoryPressureMemoryUsagePct,

    /// <summary>
    /// Gen2 collections per second recorded when Dynamic Instrumentation memory pressure changes state.
    /// </summary>
    [TelemetryMetric<MetricTags.DebuggerMemoryPressureState>("memory_pressure.gen2_per_sec", isCommon: true, NS.LiveDebugger)] DebuggerMemoryPressureGen2PerSec,

    /// <summary>
    /// Duration of Dynamic Instrumentation high-memory-pressure periods, recorded on exit.
    /// </summary>
    [TelemetryMetric("memory_pressure.duration_ms", isCommon: true, NS.LiveDebugger)] DebuggerMemoryPressureDurationMs,
#endregion
}
