// <copyright file="FlagEvalEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal sealed class FlagEvalEvent
{
    public FlagEvalEvent(string flagKey, string? variant, string? allocationKey, string? targetingKey, long evalTimeMs, IReadOnlyDictionary<string, object?>? contextAttrs, string? errorCode = null, bool observeFullEvaluationData = false)
    {
        FlagKey = flagKey;
        Variant = variant;
        AllocationKey = allocationKey;
        TargetingKey = targetingKey;
        EvalTimeMs = evalTimeMs;
        if (observeFullEvaluationData)
        {
            ContextAttrs = FlagEvaluationContext.Copy(contextAttrs, out var omissions);
            ContextOmissions = omissions;
        }

        ErrorCode = errorCode;
        ObserveFullEvaluationData = observeFullEvaluationData;
    }

    public string FlagKey { get; }

    public string? Variant { get; }

    public string? AllocationKey { get; }

    public string? TargetingKey { get; }

    public long EvalTimeMs { get; }

    public IReadOnlyDictionary<string, object?>? ContextAttrs { get; }

    public string? ErrorCode { get; }

    public bool ObserveFullEvaluationData { get; }

    public ContextOmissionReason ContextOmissions { get; }
}
