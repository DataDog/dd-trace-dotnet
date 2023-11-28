// <copyright file="ForceFlushAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// System.Threading.Tasks.Task Datadog.Trace.Tracer::ForceFlushAsync() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "ForceFlushAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = new string[0],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ForceFlushAsyncIntegration
{
    internal static CallTargetReturn<Task> OnMethodEnd<TTarget>(TTarget instance, Task returnValue, Exception exception, in CallTargetState state)
        where TTarget : ITracerProxy
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_ForceFlushAsync);

        var tracer = (Datadog.Trace.Tracer)instance.AutomaticTracer;
        return new CallTargetReturn<Task>(tracer.FlushAsync());
    }
}
