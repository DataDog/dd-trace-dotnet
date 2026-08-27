// <copyright file="DrainResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>Result returned by <see cref="FlagEvaluationAggregator.Drain"/> containing both aggregation maps and the drop counter.</summary>
internal sealed class DrainResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrainResult"/> class.
    /// </summary>
    public DrainResult(
        Dictionary<FullKey, EvaluationEntry> full,
        Dictionary<DegradedKey, EvaluationEntry> degraded,
        long dropped)
    {
        Full = full;
        Degraded = degraded;
        Dropped = dropped;
    }

    /// <summary>Gets the full-tier aggregation map (drained).</summary>
    public Dictionary<FullKey, EvaluationEntry> Full { get; }

    /// <summary>Gets the degraded-tier aggregation map (drained).</summary>
    public Dictionary<DegradedKey, EvaluationEntry> Degraded { get; }

    /// <summary>Gets the number of evaluations dropped due to degraded tier overflow.</summary>
    public long Dropped { get; }
}
