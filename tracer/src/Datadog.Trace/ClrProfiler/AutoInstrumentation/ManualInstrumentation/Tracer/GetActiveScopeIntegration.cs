﻿// <copyright file="GetActiveScopeIntegration.cs" company="Datadog">
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
/// Datadog.Trace.IScope Datadog.Trace.Tracer::get_ActiveScope() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Tracer",
    MethodName = "get_ActiveScope",
    ReturnTypeName = "Datadog.Trace.IScope",
    ParameterTypeNames = new string[0],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class GetActiveScopeIntegration
{
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        where TTarget : ITracerProxy
    {
        // TODO: Add telemetry for public API?
        var tracer = (Datadog.Trace.Tracer)instance.AutomaticTracer;
        var scope = tracer.ActiveScope;

        // The manual instrumentation returns null by default, so can re-use the return value here
        // (Not ideal for clarity, but generics prevent returning null directly)
        return new CallTargetReturn<TReturn>(state.Scope.DuckCast<TReturn>());
    }
}
