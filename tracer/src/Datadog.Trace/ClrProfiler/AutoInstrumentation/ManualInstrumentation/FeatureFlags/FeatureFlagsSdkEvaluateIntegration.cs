// <copyright file="FeatureFlagsSdkEvaluateIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    ParameterTypeNames = [ClrNames.String, "Datadog.Trace.FeatureFlags.ValueType", ClrNames.Object, ClrNames.String, "System.Collections.Generic.IDictionary`2[System.String,System.Object]"],
    MinimumVersion = "3.31.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.DatadogTraceManual))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class FeatureFlagsSdkEvaluateIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref string flagKey, FeatureFlags.ValueType targetType, ref object? defaultValue, ref string? targetingKey, ref IDictionary<string, object?>? attributes)
    {
        return new CallTargetState(null, new State(flagKey, targetType, defaultValue, targetingKey, attributes));
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception is not null)
        {
            // invalid call to the API e.g. non-null args were null. Just let it bubble up.
            return new CallTargetReturn<TReturn?>(returnValue);
        }

        var tracer = Datadog.Trace.Tracer.Instance;
        if (tracer.TracerManager.FeatureFlags is not { } flags)
        {
            // Feature flags disabled: don't touch async-local scope state for a no-op.
            return new CallTargetReturn<TReturn?>(returnValue);
        }

        var parameters = (State)state.State!;
        var res = flags.Evaluate(parameters.FlagKey, parameters.TargetType, parameters.DefaultValue, parameters.TargetingKey ?? string.Empty, parameters.Attributes);
        var traceContext = tracer.InternalActiveScope?.Span?.Context.TraceContext;

        // Skip creating per-trace state for evaluations that would record nothing.
        if (traceContext is not null && SpanEnrichmentState.IsRecordable(res))
        {
            traceContext.GetOrCreateFeatureFlagEnrichment()?.AccumulateEvaluation(res, parameters.TargetingKey);
        }

        return new CallTargetReturn<TReturn?>(res.DuckCast<TReturn>());
    }

    private sealed record State(string FlagKey, FeatureFlags.ValueType TargetType, object? DefaultValue, string? TargetingKey, IDictionary<string, object?>? Attributes)
    {
    }
}
