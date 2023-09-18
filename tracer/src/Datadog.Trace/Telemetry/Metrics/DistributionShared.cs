// <copyright file="DistributionShared.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;

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
}
