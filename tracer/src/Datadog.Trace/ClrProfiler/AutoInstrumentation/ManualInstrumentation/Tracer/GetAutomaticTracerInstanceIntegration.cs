// <copyright file="GetAutomaticTracerInstanceIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// System.Object Datadog.Trace.Tracer::GetAutomaticTracerInstance() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "GetAutomaticTracerInstance",
    ReturnTypeName = ClrNames.Object,
    ParameterTypeNames = new string[0],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class GetAutomaticTracerInstanceIntegration
{
    internal static CallTargetReturn<object> OnMethodEnd<TTarget>(object returnValue, Exception exception, in CallTargetState state)
    {
        // Used by Datadog.Trace.Manual.Tracer.Instance to create a new Datadog.Trace.Manual.Tracer instance
        return new CallTargetReturn<object>(Datadog.Trace.Tracer.Instance);
    }
}
