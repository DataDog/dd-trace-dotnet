// <copyright file="StartActiveOperationNameIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// Datadog.Trace.IScope Datadog.Trace.Tracer::StartActive(System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "StartActive",
    ReturnTypeName = "Datadog.Trace.IScope",
    ParameterTypeNames = new[] { ClrNames.String },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class StartActiveOperationNameIntegration
{
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
    /// <param name="operationName">Instance of System.String</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref string operationName)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_StartActive);
        TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.Manual);

        // The actual handling of the integration is done in the multi-arg version
        return CallTargetState.GetDefault();
    }
}
