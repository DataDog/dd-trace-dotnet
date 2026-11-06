// <copyright file="OpenFeatureSdkEvaluateIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>
/// Datadog.Trace.FeatureFlags.IEvaluation Datadog.Trace.FeatureFlags.FeatureFlagsSdk::Evaluate(System.String,System.Type,System.Object,Datadog.Trace.FeatureFlags.IEvaluationContext) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "Evaluate",
    ReturnTypeName = "Datadog.Trace.FeatureFlags.IEvaluation",
    ParameterTypeNames = [ClrNames.String, "Datadog.Trace.FeatureFlags.ValueType", ClrNames.Object, ClrNames.String, "System.Collections.Generic.IDictionary`2[System.String,System.Object]"],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkEvaluateIntegration
{
    private static readonly ConditionalWeakTable<Type, BridgeAvailability> BridgeAvailabilityByType = new();

    internal static CallTargetState OnMethodBegin<TTarget>(ref string flagKey, FeatureFlags.ValueType targetType, ref object? defaultValue, ref string? targetingKey, ref IDictionary<string, object?>? attributes)
    {
        return new CallTargetState(null, new State(flagKey, targetType, defaultValue, targetingKey, attributes, GetContextOmissionReasons(attributes)));
    }

    internal static int GetContextOmissionReasons(IDictionary<string, object?>? attributes)
    {
        try
        {
            if (attributes is not null && attributes.TryDuckCast<ContextAttributesWithOmissions>(out var contextAttributes))
            {
                return contextAttributes.ContextOmissionReasons;
            }
        }
        catch
        {
            // Context omission telemetry is best effort and must not affect flag resolution.
        }

        return 0;
    }

    internal static bool HasExposureCommitBridge(Type targetType)
        => GetBridgeAvailability(targetType).HasExposureCommitBridge;

    internal static bool HasEvaluationEventBridge(Type targetType)
        => GetBridgeAvailability(targetType).HasEvaluationEventBridge;

    private static BridgeAvailability GetBridgeAvailability(Type targetType)
        => BridgeAvailabilityByType.GetValue(
            targetType,
            static type => new BridgeAvailability(
                type.Assembly.GetType("Datadog.FeatureFlags.OpenFeature.ExposureCommitBridge", throwOnError: false) is not null,
                type.Assembly.GetType("Datadog.FeatureFlags.OpenFeature.FlagEvalEVPHook", throwOnError: false) is not null));

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception is not null)
        {
            // invalid call to the API e.g. non-null args were null. Just let it bubble up.
            return new CallTargetReturn<TReturn?>(returnValue);
        }

        return new CallTargetReturn<TReturn?>(Evaluate(
            (State)state.State!,
            deferExposure: false,
            reportRootEvaluation: !HasEvaluationEventBridge(typeof(TTarget))).DuckCast<TReturn>());
    }

    internal static FeatureFlags.Evaluation? Evaluate(State parameters, bool deferExposure, bool reportRootEvaluation)
    {
        var flags = TracerManager.Instance.FeatureFlags;

        // Fallback activation: if the user calls Evaluate without InitializeAsync (for example,
        // via the manual API or an older OpenFeature package), start delivery on first evaluation.
        flags?.Activate();

        return flags?.Evaluate(
            parameters.FlagKey,
            parameters.TargetType,
            parameters.DefaultValue,
            parameters.TargetingKey,
            parameters.Attributes,
            deferExposure,
            reportRootEvaluation,
            contextOmissionReasons: parameters.ContextOmissionReasons);
    }

    [DuckCopy]
    private struct ContextAttributesWithOmissions
    {
#pragma warning disable CS0649 // Duck typing populates this field.
        public int ContextOmissionReasons;
#pragma warning restore CS0649
    }

    private sealed record BridgeAvailability(bool HasExposureCommitBridge, bool HasEvaluationEventBridge);

    internal sealed record State(string FlagKey, FeatureFlags.ValueType TargetType, object? DefaultValue, string? TargetingKey, IDictionary<string, object?>? Attributes, int ContextOmissionReasons)
    {
    }
}
