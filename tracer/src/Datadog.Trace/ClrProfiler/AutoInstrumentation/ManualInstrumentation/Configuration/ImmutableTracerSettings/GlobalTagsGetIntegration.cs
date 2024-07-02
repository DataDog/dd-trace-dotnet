// <copyright file="GlobalTagsGetIntegration.cs" company="Datadog">
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
/// System.Collections.Generic.IReadOnlyDictionary`2[System.String,System.String] Datadog.Trace.Configuration.ImmutableTracerSettings::get_GrpcTags() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.ImmutableTracerSettings",
    MethodName = "get_GlobalTags",
    ReturnTypeName = "System.Collections.Generic.IReadOnlyDictionary`2[System.String,System.String]",
    ParameterTypeNames = [],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class GlobalTagsGetIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableTracerSettings_GlobalTags_Get);
        return CallTargetState.GetDefault();
    }
}
