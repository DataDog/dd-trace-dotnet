// <copyright file="FlagEvaluationAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

// The background worker is the sole owner of Add and Drain.
internal sealed class FlagEvaluationAggregator
{
    private readonly int _globalCap;
    private readonly int _perFlagCap;
    private readonly int _degradedCap;
    private readonly Dictionary<string, int> _perFlag = new(StringComparer.Ordinal);
    private Dictionary<FullKey, EvaluationEntry> _full = [];
    private Dictionary<DegradedKey, EvaluationEntry> _degraded = [];
    private long _dropped;
    private long _invalidTargetingKeys;

    public FlagEvaluationAggregator(int globalCap, int perFlagCap, int degradedCap)
    {
        _globalCap = Math.Max(0, globalCap);
        _perFlagCap = Math.Max(0, perFlagCap);
        _degradedCap = Math.Max(0, degradedCap);
    }

    public void Add(FlagEvalEvent observation)
    {
        var targetingKey = observation.TargetingKey;
        if (targetingKey is not null && !FlagEvaluationPrivacy.IsValidText(targetingKey))
        {
            targetingKey = null;
            _invalidTargetingKeys++;
        }

        // Revalidate independently of the provider and event constructor. Protected observations
        // never enumerate attributes, and canonicalization only sorts already-bounded data.
        var consent = observation.ObserveFullEvaluationData;
        var attrs = consent ? FlagEvaluationContext.Copy(observation.ContextAttrs, out _) : null;
        var contextKey = FlagEvaluationContext.CanonicalKey(attrs);
        var dimensions = new DegradedKey(observation.FlagKey, observation.Variant, observation.AllocationKey, FlagEvaluationPrivacy.ErrorCodeForOutput(observation.ErrorCode));
        var fullKey = new FullKey(dimensions, targetingKey, contextKey, consent);
        if (_full.TryGetValue(fullKey, out var existing))
        {
            existing.Observe(observation.EvalTimeMs, consent);
            return;
        }

        _perFlag.TryGetValue(observation.FlagKey, out var perFlagCount);
        if (_full.Count < _globalCap && perFlagCount < _perFlagCap)
        {
            _full.Add(fullKey, new EvaluationEntry(observation.EvalTimeMs, consent, attrs));
            // Only track flags with retained full buckets, so this map is bounded too.
            _perFlag[observation.FlagKey] = perFlagCount + 1;
            return;
        }

        if (_degraded.TryGetValue(dimensions, out existing))
        {
            existing.Observe(observation.EvalTimeMs, consent);
        }
        else if (_degraded.Count < _degradedCap)
        {
            _degraded.Add(dimensions, new EvaluationEntry(observation.EvalTimeMs, consent, null));
        }
        else
        {
            _dropped++;
        }
    }

    public DrainResult Drain()
    {
        var result = new DrainResult(_full, _degraded, _dropped, _invalidTargetingKeys);
        _full = [];
        _degraded = [];
        _perFlag.Clear();
        _dropped = 0;
        _invalidTargetingKeys = 0;
        return result;
    }
}
