// <copyright file="StartActiveSpanCreationSettingsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// Datadog.Trace.IScope Datadog.Trace.Tracer::StartActive(System.String,Datadog.Trace.SpanCreationSettings) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "StartActive",
    ReturnTypeName = "Datadog.Trace.IScope",
    ParameterTypeNames = [ClrNames.String, "Datadog.Trace.SpanCreationSettings"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class StartActiveSpanCreationSettingsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSettings>(TTarget instance, string operationName, in TSettings settings)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_StartActive_Settings);
        TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.Manual);

        // The actual handling of the integration is done in the multi-arg version
        return CallTargetState.GetDefault();
    }
}
