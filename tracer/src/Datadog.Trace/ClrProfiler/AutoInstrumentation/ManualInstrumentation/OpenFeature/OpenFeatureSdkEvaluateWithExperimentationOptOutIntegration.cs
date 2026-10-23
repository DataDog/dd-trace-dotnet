// <copyright file="OpenFeatureSdkEvaluateWithExperimentationOptOutIntegration.cs" company="Datadog">
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

/// <summary>Evaluates with the provider's resolved experimentation opt-out mode.</summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "EvaluateWithExperimentationOptOut",
    ReturnTypeName = "Datadog.Trace.FeatureFlags.IEvaluation",
    ParameterTypeNames = [ClrNames.String, "Datadog.Trace.FeatureFlags.ValueType", ClrNames.Object, ClrNames.String, "System.Collections.Generic.IDictionary`2[System.String,System.Object]", ClrNames.Bool],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkEvaluateWithExperimentationOptOutIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref string flagKey, FeatureFlags.ValueType targetType, ref object? defaultValue, ref string? targetingKey, ref IDictionary<string, object?>? attributes, bool immediate)
        => new(null, new State(flagKey, targetType, defaultValue, targetingKey, attributes, immediate));

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception is not null)
        {
            return new(returnValue);
        }

        var parameters = (State)state.State!;
        var flags = TracerManager.Instance.FeatureFlags;
        flags?.Activate();
        var result = flags?.Evaluate(parameters.FlagKey, parameters.TargetType, parameters.DefaultValue, parameters.TargetingKey, parameters.Attributes, parameters.Immediate);
        return new(result.DuckCast<TReturn>());
    }

    private sealed record State(string FlagKey, FeatureFlags.ValueType TargetType, object? DefaultValue, string? TargetingKey, IDictionary<string, object?>? Attributes, bool Immediate);
}
