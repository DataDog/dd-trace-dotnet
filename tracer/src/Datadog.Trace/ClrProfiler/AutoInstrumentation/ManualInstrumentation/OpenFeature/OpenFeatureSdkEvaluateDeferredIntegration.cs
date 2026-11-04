// <copyright file="OpenFeatureSdkEvaluateDeferredIntegration.cs" company="Datadog">
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

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>
/// Evaluates like Evaluate, but keeps the exposure pending until the provider commits it.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "EvaluateDeferred",
    ReturnTypeName = "Datadog.Trace.FeatureFlags.IEvaluation",
    ParameterTypeNames = [ClrNames.String, "Datadog.Trace.FeatureFlags.ValueType", ClrNames.Object, ClrNames.String, "System.Collections.Generic.IDictionary`2[System.String,System.Object]"],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkEvaluateDeferredIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref string flagKey, FeatureFlags.ValueType targetType, ref object? defaultValue, ref string? targetingKey, ref IDictionary<string, object?>? attributes)
        => new(scope: null, new OpenFeatureSdkEvaluateIntegration.State(flagKey, targetType, defaultValue, targetingKey, attributes));

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception is not null)
        {
            return new CallTargetReturn<TReturn?>(returnValue);
        }

        return new CallTargetReturn<TReturn?>(OpenFeatureSdkEvaluateIntegration.Evaluate((OpenFeatureSdkEvaluateIntegration.State)state.State!, deferExposure: true).DuckCast<TReturn>());
    }
}
