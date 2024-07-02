// <copyright file="StartSpanIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// Datadog.Trace.ISpan Datadog.Trace.Tracer::Datadog.Trace.IDatadogOpenTracingTracer.StartSpan(System.String,Datadog.Trace.ISpanContext,System.String,System.Nullable`1[System.DateTimeOffset],System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "Datadog.Trace.IDatadogOpenTracingTracer.StartSpan",
    ReturnTypeName = "Datadog.Trace.ISpan",
    ParameterTypeNames = [ClrNames.String, "Datadog.Trace.ISpanContext", ClrNames.String, "System.Nullable`1[System.DateTimeOffset]", ClrNames.Bool],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class StartSpanIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSpanContext>(TTarget instance, string operationName, TSpanContext parent, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope)
        where TTarget : ITracerProxy
    {
        // This is only used by the OpenTracing public API

        // parent should _normally_ be a manual span, unless they've created a "custom" ISpanContext
        var parentContext = SpanContextHelper.GetContext(parent);

        var tracer = (Datadog.Trace.Tracer)instance.AutomaticTracer;
        var span = ((IDatadogOpenTracingTracer)tracer).StartSpan(operationName, parentContext, serviceName, startTime, ignoreActiveScope);
        tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(ManualInstrumentationConstants.Id);

        return new CallTargetState(scope: null, state: span);
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        // Duck cast Span as an ISpan (DataDog.Trace.Manual) and return it
        return new CallTargetReturn<TReturn>(state.State.DuckCast<TReturn>());
    }
}
