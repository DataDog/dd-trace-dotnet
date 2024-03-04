// <copyright file="GetDefaultServiceNameIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// System.String Datadog.Trace.Tracer::get_DefaultServiceName() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "get_DefaultServiceName",
    ReturnTypeName = ClrNames.String,
    ParameterTypeNames = new string[0],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class GetDefaultServiceNameIntegration
{
    internal static CallTargetReturn<string> OnMethodEnd<TTarget>(TTarget instance, string returnValue, Exception exception, in CallTargetState state)
        where TTarget : ITracerProxy
    {
        // TODO: Add telemetry?
        var tracer = (Datadog.Trace.Tracer)instance.AutomaticTracer;
        return new CallTargetReturn<string>(tracer.DefaultServiceName);
    }
}
