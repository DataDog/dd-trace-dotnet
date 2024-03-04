// <copyright file="OpenTracingTracerFactoryWrapTracerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenTracing;

/// <summary>
/// OpenTracing.ITracer Datadog.Trace.OpenTracing.OpenTracingTracerFactory::WrapTracer(Datadog.Trace.Tracer) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.OpenTracing",
    TypeName = "Datadog.Trace.OpenTracing.OpenTracingTracerFactory",
    MethodName = "WrapTracer",
    ReturnTypeName = "OpenTracing.ITracer",
    ParameterTypeNames = ["Datadog.Trace.Tracer"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class OpenTracingTracerFactoryWrapTracerIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TTracer>(ref TTracer tracer)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.OpenTracingTracerFactory_WrapTracer);
        return CallTargetState.GetDefault();
    }
}
