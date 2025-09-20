// <copyright file="AddRootSpanFilterIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// System.Void Datadog.Trace.Tracer::AddRootSpanFilter(System.Func{Datadog.Trace.ISpan,System.Boolean}) calltarget instrumentation
/// </summary>
#if NETFRAMEWORK
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "AddRootSpanFilter",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Func`2[[Datadog.Trace.ISpan, Datadog.Trace.Manual],[System.Boolean, mscorlib]]"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
#else
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "AddRootSpanFilter",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Func`2[[Datadog.Trace.ISpan, Datadog.Trace.Manual],[System.Boolean, System.Private.CoreLib]]"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
#endif
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class AddRootSpanFilterIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSpanFilter>(TTarget instance, TSpanFilter shouldRejectTrace)
        where TTarget : ITracerProxy
        where TSpanFilter : Delegate
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_AddRootSpanFilter);

        if (instance.AutomaticTracer is Datadog.Trace.Tracer tracer)
        {
            if (shouldRejectTrace is Func<ISpan, bool> typedFilter)
            {
                tracer.AddRootSpanFilterInternal(typedFilter);
            }
        }

        return CallTargetState.GetDefault();
    }
}
