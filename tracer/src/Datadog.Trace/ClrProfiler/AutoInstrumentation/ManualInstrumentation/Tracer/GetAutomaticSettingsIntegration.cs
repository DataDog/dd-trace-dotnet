// <copyright file="GetAutomaticSettingsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;

/// <summary>
/// Datadog.Trace.Configuration.ITracerSettings Datadog.Trace.Tracer::GetAutomaticSettings(System.Object) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "GetAutomaticSettings",
    ReturnTypeName = "Datadog.Trace.Configuration.ITracerSettings",
    ParameterTypeNames = [ClrNames.Object],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class GetAutomaticSettingsIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(object automaticTracer)
    {
        if (automaticTracer is Datadog.Trace.Tracer tracer)
        {
            return new CallTargetState(scope: null, state: automaticTracer);
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, in CallTargetState state)
    {
        return new CallTargetReturn<TReturn>(state.State is null ? returnValue : state.State.DuckCast<TReturn>());
    }
}
