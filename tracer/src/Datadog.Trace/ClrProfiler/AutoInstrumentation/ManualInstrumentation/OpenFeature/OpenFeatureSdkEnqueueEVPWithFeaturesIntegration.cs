// <copyright file="OpenFeatureSdkEnqueueEVPWithFeaturesIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.FlagEvaluation;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>
/// Hands a provider-captured evaluation and the split's EVALUATION features to the tracer's bounded
/// event queue. The provider calls this only for evaluations that carry such features, because the
/// ninth argument takes CallTarget's allocating slow path.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "EnqueueEVPWithFeatures",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.Int64, ClrNames.String, "System.Collections.Generic.IReadOnlyDictionary`2[System.String,System.Object]", "System.Collections.Generic.IReadOnlyDictionary`2[System.String,System.Object]", ClrNames.Int32],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkEnqueueEVPWithFeaturesIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TFlagKey, TVariant, TAllocationKey, TTargetingKey, TEvalTimeMs, TErrorCode, TAttrs, TFeatures, TFlags>(TFlagKey flagKey, TVariant variant, TAllocationKey allocationKey, TTargetingKey targetingKey, TEvalTimeMs evalTimeMs, TErrorCode errorCode, TAttrs attrs, TFeatures features, TFlags flags)
    {
        try
        {
            Enqueue(TracerManager.Instance.FeatureFlags?.EvaluationWriter, flagKey, variant, allocationKey, targetingKey, evalTimeMs, errorCode, attrs, features, flags);
        }
        catch (Exception)
        {
            OpenFeatureSdkRecordEVPHookErrorIntegration.RecordError();
        }

        return CallTargetState.GetDefault();
    }

    // Takes the writer, so tests need no global tracer.
    internal static void Enqueue<TFlagKey, TVariant, TAllocationKey, TTargetingKey, TEvalTimeMs, TErrorCode, TAttrs, TFeatures, TFlags>(FlagEvaluationWriter? writer, TFlagKey flagKey, TVariant variant, TAllocationKey allocationKey, TTargetingKey targetingKey, TEvalTimeMs evalTimeMs, TErrorCode errorCode, TAttrs attrs, TFeatures features, TFlags flags)
        => OpenFeatureSdkEnqueueEVPIntegration.Enqueue(
            writer,
            (string)(object)flagKey!,
            (string?)(object?)variant,
            (string?)(object?)allocationKey,
            (string?)(object?)targetingKey,
            (long)(object)evalTimeMs!,
            (string?)(object?)errorCode,
            (IReadOnlyDictionary<string, object?>?)(object?)attrs,
            (int)(object)flags!,
            (IReadOnlyDictionary<string, object?>?)(object?)features);
}
