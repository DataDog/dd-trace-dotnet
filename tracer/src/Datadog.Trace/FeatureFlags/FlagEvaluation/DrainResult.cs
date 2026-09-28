// <copyright file="DrainResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal sealed class DrainResult(
    Dictionary<FullKey, EvaluationEntry> full,
    Dictionary<DegradedKey, EvaluationEntry> degraded,
    long dropped,
    long invalidTargetingKeys = 0)
{
    public Dictionary<FullKey, EvaluationEntry> Full { get; } = full;

    public Dictionary<DegradedKey, EvaluationEntry> Degraded { get; } = degraded;

    public long Dropped { get; } = dropped;

    public long InvalidTargetingKeys { get; } = invalidTargetingKeys;
}
