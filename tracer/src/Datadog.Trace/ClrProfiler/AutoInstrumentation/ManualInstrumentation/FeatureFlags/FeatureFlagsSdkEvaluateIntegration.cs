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
    ParameterTypeNames = [ClrNames.String, "Datadog.Trace.FeatureFlags.ValueType", ClrNames.Object, "Datadog.Trace.FeatureFlags.IEvaluationContext"],
    MinimumVersion = "3.31.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.DatadogTraceManual))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class FeatureFlagsSdkEvaluateIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TTargetType, TContext>(ref string? key, ref TTargetType targetType, ref object? defaultValue, ref TContext? context)
    {
        return new CallTargetState(null, new State(key, (FeatureFlags.ValueType)(Convert.ToInt32(targetType)), defaultValue, context));
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        var parameters = (State)state.State!;
        var res = TracerManager.Instance.FeatureFlags?.Evaluate(parameters.Key!, parameters.TargetType, parameters.DefaultValue, parameters.Context.DuckCast<IEvaluationContext>()!);
        return new CallTargetReturn<TReturn?>(res.DuckCast<TReturn>());
    }

    private sealed record State(string? Key, FeatureFlags.ValueType TargetType, object? DefaultValue, object? Context)
    {
    }
}
