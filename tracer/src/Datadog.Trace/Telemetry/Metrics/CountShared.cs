// <copyright file="CountShared.cs" company="Datadog">
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
[TelemetryMetricType(TelemetryMetricType.Count, isCiVisibilityMetric: true, isApmMetric: true)]
internal enum CountShared
{
#region Tracers Namespace

    /// <summary>
    /// The number of errors/failures in the library integration, tagged by the integration name (e.g. `integration_name:kafka`, `integration_name:rabbitmq`) and ErrorType (e.g. `error:duck_type`, `error:runtime`). Both tags will vary by implementation language",
    /// </summary>
    [TelemetryMetric<MetricTags.IntegrationName, MetricTags.InstrumentationError>("integration_errors")] IntegrationsError,
#endregion
}
