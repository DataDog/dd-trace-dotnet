// <copyright file="AgentUriIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.ImmutableTracerSettings;

/// <summary>
/// System.Uri Datadog.Trace.Configuration.ImmutableTracerSettings::get_AgentUri() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.ImmutableTracerSettings",
    MethodName = "get_AgentUri",
    ReturnTypeName = "System.Uri",
    ParameterTypeNames = [],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class AgentUriIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        // Ok, this public API is a _bit_ weird now, because it's not what we're actually
        // instrumenting, but for now this is the easiest, and avoids "duplicate" telemetry
        TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableExporterSettings_AgentUri_Get);
        return CallTargetState.GetDefault();
    }
}
