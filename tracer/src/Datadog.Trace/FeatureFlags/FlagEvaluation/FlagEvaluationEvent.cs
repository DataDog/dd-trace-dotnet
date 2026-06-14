// <copyright file="FlagEvaluationEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// One flag evaluation event (one aggregation bucket). Required fields are always present;
/// optional fields are null so NullValueHandling.Ignore omits them in JSON (schema conformance).
/// </summary>
internal sealed class FlagEvaluationEvent
{
    /// <summary>Gets or sets the flush timestamp in Unix milliseconds.</summary>
    public long Timestamp { get; set; }

    /// <summary>Gets or sets the flag reference.</summary>
    public FlagEvalFlag Flag { get; set; } = default!;

    /// <summary>Gets or sets the first evaluation timestamp in this window (Unix ms, min across bucket).</summary>
    public long FirstEvaluation { get; set; }

    /// <summary>Gets or sets the last evaluation timestamp in this window (Unix ms, max across bucket).</summary>
    public long LastEvaluation { get; set; }

    /// <summary>Gets or sets the number of evaluations in this window.</summary>
    public long EvaluationCount { get; set; }

    /// <summary>Gets or sets a value indicating whether the evaluation returned the runtime default (absent variant). Present only when true.</summary>
    public bool? RuntimeDefault { get; set; }

    /// <summary>Gets or sets the targeting key. Full tier only — null on degraded tier (omitted by NullValueHandling.Ignore).</summary>
    public string? TargetingKey { get; set; }

    /// <summary>Gets or sets the variant. Absent when variant was null (runtime_default_used).</summary>
    public FlagEvalVariant? Variant { get; set; }

    /// <summary>Gets or sets the allocation. Absent when allocationKey is null/empty.</summary>
    public FlagEvalAllocation? Allocation { get; set; }

    /// <summary>Gets or sets the per-event context. Full tier only — null on degraded tier (omitted by NullValueHandling.Ignore).</summary>
    public FlagEvalEventContext? Context { get; set; }
}
