// <copyright file="SpanContextInjectorConstructorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Propagators;

/// <summary>
/// System.Void Datadog.Trace.SpanContextInjector::.ctor() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.SpanContextInjector",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new string[0],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SpanContextInjectorConstructorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.SpanContextInjector_Ctor);
        return CallTargetState.GetDefault();
    }
}
