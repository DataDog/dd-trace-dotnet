// <copyright file="OpenFeatureSdkEnqueueEVPIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.FlagEvaluation;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>
/// System.Void Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk::EnqueueEVP(...) calltarget instrumentation.
/// Routes the EVP enqueue call from FlagEvalEVPHook (in Datadog.FeatureFlags.OpenFeature NuGet)
/// through to FlagEvaluationApi in the full tracer (Datadog.Trace.dll).
/// This is the cross-assembly bridge that avoids a package reference from the NuGet to Datadog.Trace.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "EnqueueEVP",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String, "System.String", "System.String", "System.String", "System.String", ClrNames.Int64, "System.Collections.Generic.IDictionary`2[System.String,System.Object]"],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkEnqueueEVPIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(
        ref string flagKey,
        ref string? variant,
        ref string? allocationKey,
        ref string? targetingKey,
        ref string? errorMessage,
        ref long evalTimeMs,
        ref IDictionary<string, object?>? contextAttrs)
    {
        var api = TracerManager.Instance.FeatureFlags?.GetEVPApi();
        if (api is not null)
        {
            Dictionary<string, object?>? ctxDict = contextAttrs is Dictionary<string, object?> d
                ? d
                : contextAttrs is not null ? new Dictionary<string, object?>(contextAttrs) : null;

            api.Enqueue(new FlagEvalEvent(
                flagKey: flagKey,
                variant: variant,
                allocationKey: allocationKey,
                targetingKey: targetingKey,
                evalTimeMs: evalTimeMs,
                contextAttrs: ctxDict,
                errorMessage: errorMessage));
        }

        return CallTargetState.GetDefault();
    }
}
