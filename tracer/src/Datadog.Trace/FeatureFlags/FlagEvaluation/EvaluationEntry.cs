// <copyright file="EvaluationEntry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// Per-bucket aggregation entry: count + first/last evaluation timestamps + tier-specific data.
/// </summary>
internal sealed class EvaluationEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationEntry"/> class.
    /// </summary>
    public EvaluationEntry(long evalTimeMs, bool runtimeDefault, Dictionary<string, object?>? contextAttrs)
    {
        Count = 1;
        FirstEvaluationMs = evalTimeMs;
        LastEvaluationMs = evalTimeMs;
        RuntimeDefault = runtimeDefault;
        ContextAttrs = contextAttrs;
    }

    /// <summary>Gets the number of evaluations in this bucket.</summary>
    public long Count { get; private set; }

    /// <summary>Gets the minimum (first) eval timestamp in this window (Unix milliseconds).</summary>
    public long FirstEvaluationMs { get; private set; }

    /// <summary>Gets the maximum (last) eval timestamp in this window (Unix milliseconds).</summary>
    public long LastEvaluationMs { get; private set; }

    /// <summary>Gets a value indicating whether the evaluation used the runtime default (absent variant).</summary>
    public bool RuntimeDefault { get; }

    /// <summary>Gets the pruned context attributes (full tier only; null for degraded tier).</summary>
    public Dictionary<string, object?>? ContextAttrs { get; }

    /// <summary>
    /// Records one more evaluation against this bucket: increments count, widens the time window.
    /// </summary>
    public void Observe(long evalTimeMs)
    {
        Count++;
        if (evalTimeMs < FirstEvaluationMs)
        {
            FirstEvaluationMs = evalTimeMs;
        }

        if (evalTimeMs > LastEvaluationMs)
        {
            LastEvaluationMs = evalTimeMs;
        }
    }
}
