// <copyright file="OpenFeatureSdkEnqueueEVPIntegration.cs" company="Datadog">
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
/// Hands provider-captured evaluation metadata to the tracer's bounded event queue.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "EnqueueEVP",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.Int64, ClrNames.String, "System.Collections.Generic.IReadOnlyDictionary`2[System.String,System.Object]", ClrNames.Int32],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkEnqueueEVPIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref string flagKey, ref string? variant, ref string? allocationKey, ref string? targetingKey, ref long evalTimeMs, ref string? errorCode, ref IReadOnlyDictionary<string, object?>? attrs, ref int flags)
    {
        try
        {
            Enqueue(TracerManager.Instance.FeatureFlags?.EvaluationWriter, flagKey, variant, allocationKey, targetingKey, evalTimeMs, errorCode, attrs, flags);
        }
        catch (Exception)
        {
            OpenFeatureSdkRecordEVPHookErrorIntegration.RecordError();
        }

        return CallTargetState.GetDefault();
    }

    internal static void Enqueue(FlagEvaluationWriter? writer, string flagKey, string? variant, string? allocationKey, string? targetingKey, long evalTimeMs, string? errorCode, IReadOnlyDictionary<string, object?>? attrs, int flags)
    {
        if (writer is null)
        {
            return;
        }

        try
        {
            // Constructor owns a bounded detached copy only with consent. Snapshot failures
            // become a context omission, retaining the otherwise valid evaluation.
            var consent = (flags & FlagEvaluationBridge.ObserveFullEvaluationData) != 0;
            var observation = new FlagEvalEvent(flagKey, variant, allocationKey, targetingKey, evalTimeMs, attrs, errorCode, consent);
            writer.TryEnqueue(observation, flags & ~FlagEvaluationBridge.ObserveFullEvaluationData);
        }
        catch (Exception)
        {
            OpenFeatureSdkRecordEVPHookErrorIntegration.RecordError();
        }
    }
}
