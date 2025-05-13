// <copyright file="StartActiveImplementationIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// Datadog.Trace.IScope Datadog.Trace.Tracer::StartActive(System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "StartActive",
    ReturnTypeName = "Datadog.Trace.IScope",
    ParameterTypeNames = [ClrNames.String, "Datadog.Trace.ISpanContext", ClrNames.String, "System.Nullable`1[System.DateTimeOffset]", "System.Nullable`1[System.Boolean]"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class StartActiveImplementationIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<StartActiveImplementationIntegration>();

    internal static CallTargetState OnMethodBegin<TTarget, TSpanContext>(TTarget instance, string operationName, TSpanContext? parent, string? serviceName, DateTimeOffset? startTime, bool? finishOnClose)
        where TTarget : ITracerProxy
    {
        // parent should _normally_ be a manual span, unless they've created a "custom" ISpanContext
        var parentContext = SpanContextHelper.GetContext(parent);

        if (instance.AutomaticTracer is not Datadog.Trace.Tracer tracer)
        {
            Log.Error(
                "Error: instance.AutomaticTracer is not a Datadog.Trace.Tracer: {TracerType}. This should never happen, and indicates a problem with automatic instrumentation.",
                instance.AutomaticTracer?.GetType());
            return CallTargetState.GetDefault();
        }

        var scope = tracer.StartActiveInternal(
            operationName,
            parent: parentContext,
            serviceName: serviceName,
            startTime: startTime,
            finishOnClose: finishOnClose ?? true);

        tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(ManualInstrumentationConstants.Id);
        return new CallTargetState(scope);
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        // Duck cast Scope as an IScope (DataDog.Trace.Manual) and return it
        var duckScope = state.Scope is { } scope
                            ? scope.DuckCast<TReturn>()
                            : returnValue;
        return new CallTargetReturn<TReturn>(duckScope);
    }
}
