// <copyright file="FlagEvalEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// Minimal snapshot enqueued by FlagEvalEVPHook.FinallyAsync (hot path — cheap capture only).
/// The background aggregation worker reads this off the evaluation path.
/// </summary>
internal sealed class FlagEvalEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlagEvalEvent"/> class.
    /// </summary>
    public FlagEvalEvent(
        string flagKey,
        string? variant,
        string reason,
        string? allocationKey,
        string? targetingKey,
        long evalTimeMs,
        Dictionary<string, object?>? contextAttrs)
    {
        FlagKey = flagKey;
        Variant = variant;
        Reason = reason;
        AllocationKey = allocationKey ?? string.Empty;
        TargetingKey = targetingKey ?? string.Empty;
        EvalTimeMs = evalTimeMs;
        ContextAttrs = contextAttrs;
    }

    /// <summary>Gets the flag key.</summary>
    public string FlagKey { get; }

    /// <summary>Gets the variant; null means absent variant = runtime_default_used (reviewer concern #5).</summary>
    public string? Variant { get; }

    /// <summary>Gets the evaluation reason.</summary>
    public string Reason { get; }

    /// <summary>Gets the allocation key.</summary>
    public string AllocationKey { get; }

    /// <summary>Gets the targeting key.</summary>
    public string TargetingKey { get; }

    /// <summary>Gets the eval time in Unix milliseconds from flag metadata "dd.eval.timestamp_ms", or hook-fire time.</summary>
    public long EvalTimeMs { get; }

    /// <summary>Gets the flat context attributes (already flattened by caller if needed).</summary>
    public Dictionary<string, object?>? ContextAttrs { get; }
}
