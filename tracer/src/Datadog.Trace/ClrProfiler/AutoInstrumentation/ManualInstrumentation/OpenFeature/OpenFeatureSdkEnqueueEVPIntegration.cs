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
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.Int64, ClrNames.Bool, ClrNames.String, "System.Collections.Generic.IReadOnlyDictionary`2[System.String,System.Object]", ClrNames.Int32],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkEnqueueEVPIntegration
{
    // Nine arguments use CallTarget's existing slow path, which does not support by-ref parameters.
    internal static CallTargetState OnMethodBegin<TTarget>(string flagKey, string? variant, string? allocationKey, string? targetingKey, long evalTimeMs, bool consent, string? errorCode, IReadOnlyDictionary<string, object?>? attrs, int omissionReasons)
    {
        try
        {
            Enqueue(TracerManager.Instance.FeatureFlags?.EvaluationWriter, flagKey, variant, allocationKey, targetingKey, evalTimeMs, consent, errorCode, attrs, omissionReasons);
        }
        catch (Exception)
        {
            OpenFeatureSdkRecordEVPHookErrorIntegration.RecordError();
        }

        return CallTargetState.GetDefault();
    }

    internal static void Enqueue(FlagEvaluationWriter? writer, string flagKey, string? variant, string? allocationKey, string? targetingKey, long evalTimeMs, bool consent, string? errorCode, IReadOnlyDictionary<string, object?>? attrs, int omissionReasons)
    {
        if (writer is null)
        {
            return;
        }

        try
        {
            // Constructor owns a bounded detached copy only with consent. Snapshot failures
            // become a context omission, retaining the otherwise valid evaluation.
            var observation = new FlagEvalEvent(flagKey, variant, allocationKey, targetingKey, evalTimeMs, attrs, errorCode, consent);
            writer.TryEnqueue(observation, omissionReasons);
        }
        catch (Exception)
        {
            OpenFeatureSdkRecordEVPHookErrorIntegration.RecordError();
        }
    }
}
