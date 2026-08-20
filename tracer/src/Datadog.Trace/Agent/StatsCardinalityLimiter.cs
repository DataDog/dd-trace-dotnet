// <copyright file="StatsCardinalityLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Agent;

internal sealed class StatsCardinalityLimiter
{
    private readonly Limiter<string> _resourceLimiter;
    private readonly Limiter<string> _httpEndpointLimiter;
    private readonly Limiter<ulong> _peerTagHashLimiter;
    private readonly Limiter<ulong> _additionalTagHashLimiter;

    public StatsCardinalityLimiter(TracerSettings settings)
    {
        _resourceLimiter = new(settings.StatsResourceCardinalityLimit);
        _httpEndpointLimiter = new(settings.StatsHttpEndpointCardinalityLimit);
        _peerTagHashLimiter = new(settings.StatsPeerTagsCardinalityLimit);
        _additionalTagHashLimiter = new(settings.StatsAdditionalTagsCardinalityLimit);
    }

    public void Reset()
    {
        _resourceLimiter.Reset();
        _httpEndpointLimiter.Reset();
        _peerTagHashLimiter.Reset();
        _additionalTagHashLimiter.Reset();
    }

    /// <summary>
    /// Applies cardinality limitation (using 'tracer_blocked_value' sentinel value) and replaces the
    /// <see cref="StatsAggregationKey"/> with the new limited value. Returns `true` if cardinality
    /// limits were applied to the key, false otherwise.
    /// </summary>
    public bool ApplyCardinalityLimits(ref StatsAggregationKey key)
    {
        var limitsApplied = false;
        if (_resourceLimiter.ApplyLimit(key.Resource))
        {
            limitsApplied = true;
            key = key with
            {
                Resource = StatsAggregator.BlockedByTracerSentinel,
                CardinalityLimitedFields = key.CardinalityLimitedFields | StatsCardinalityLimitedFields.Resource,
            };
        }

        if (!string.IsNullOrEmpty(key.HttpEndpoint) && _httpEndpointLimiter.ApplyLimit(key.HttpEndpoint))
        {
            limitsApplied = true;
            key = key with
            {
                HttpEndpoint = StatsAggregator.BlockedByTracerSentinel,
                CardinalityLimitedFields = key.CardinalityLimitedFields | StatsCardinalityLimitedFields.HttpEndpoint,
            };
        }

        if (key.PeerTagsHash != 0 && _peerTagHashLimiter.ApplyLimit(key.PeerTagsHash))
        {
            limitsApplied = true;
            key = key with
            {
                PeerTagsHash = 0, // We replace this with the sentinel later
                CardinalityLimitedFields = key.CardinalityLimitedFields | StatsCardinalityLimitedFields.PeerTags,
            };
        }

        if (key.AdditionalMetricTagsHash != 0 && _additionalTagHashLimiter.ApplyLimit(key.AdditionalMetricTagsHash))
        {
            limitsApplied = true;
            key = key with
            {
                AdditionalMetricTagsHash = 0, // We replace this with the sentinel later
                CardinalityLimitedFields = key.CardinalityLimitedFields | StatsCardinalityLimitedFields.AdditionalMetricTags,
            };
        }

        return limitsApplied;
    }

    private sealed class Limiter<T>(int limit)
    {
        private readonly HashSet<T> _seen = [];
        private readonly int _limit = limit;

        public bool ApplyLimit(T value)
        {
            if (_seen.Count >= _limit && !_seen.Contains(value))
            {
                return true;
            }

            _seen.Add(value);
            return false;
        }

        public void Reset() => _seen.Clear();
    }
}
