// <copyright file="FeatureFlagsSdkEvaluateIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.FeatureFlags;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Datadog_Trace_Manual;

/// <summary>
/// Datadog.Trace.FeatureFlags.IEvaluation Datadog.Trace.FeatureFlags.FeatureFlagsSdk::Evaluate(System.String,System.Type,System.Object,Datadog.Trace.FeatureFlags.IEvaluationContext) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.FeatureFlags.FeatureFlagsSdk",
    MethodName = "Evaluate",
    ReturnTypeName = "Datadog.Trace.FeatureFlags.IEvaluation",
    ParameterTypeNames = [ClrNames.String, ClrNames.Type, ClrNames.Object, "Datadog.Trace.FeatureFlags.IEvaluationContext"],
    MinimumVersion = "3.31.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.DatadogTraceManual))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class FeatureFlagsSdkEvaluateIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(ref string key, ref Type targetType, ref object? defaultValue, ref TContext? context)
    {
        return new CallTargetState(null, new State(key, targetType, defaultValue, context));
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        var parameters = (State)state.State!;
        var res = FeatureFlagsModule.Instance.Evaluate(parameters.Key!, parameters.TargetType, parameters.DefaultValue, parameters.Context.DuckCast<IEvaluationContext>()!);
        return new CallTargetReturn<TReturn?>(res.DuckCast<TReturn>());
    }

    private record struct State(string? Key, Type TargetType, object? DefaultValue, object? Context)
    {
    }
}
