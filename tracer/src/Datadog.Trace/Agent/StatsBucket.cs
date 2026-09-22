// <copyright file="StatsBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.Datadog.Sketches.Mappings;
using Datadog.Trace.Vendors.Datadog.Sketches.Stores;

namespace Datadog.Trace.Agent
{
    internal sealed class StatsBucket
    {
        public StatsBucket(StatsAggregationKey key, List<byte[]> peerTags, List<byte[]> additionalMetricTags)
        {
            Key = key;
            OkSummary = CreateSketch();
            ErrorSummary = CreateSketch();
            PeerTags = peerTags;
            AdditionalMetricTags = additionalMetricTags;
        }

        public StatsAggregationKey Key { get; }

        public long Hits { get; set; }

        public long Errors { get; set; }

        public long Duration { get; set; }

        public DDSketch OkSummary { get; }

        public DDSketch ErrorSummary { get; }

        public long TopLevelHits { get; set; }

        // Tracked for OTLP histogram data points.
        // MinDuration sentinel long.MaxValue means "not yet observed".
        // MaxDuration sentinel long.MinValue means "not yet observed".
        public long MinDuration { get; set; } = long.MaxValue;

        public long MaxDuration { get; set; } = long.MinValue;

        public List<byte[]> PeerTags { get; }

        public List<byte[]> AdditionalMetricTags { get; }

        public void Clear()
        {
            Hits = 0;
            Errors = 0;
            Duration = 0;
            TopLevelHits = 0;
            MinDuration = long.MaxValue;
            MaxDuration = long.MinValue;
            OkSummary.Clear();
            ErrorSummary.Clear();
        }

        // TODO: For OTLP, we amy want to use the AGENT_RELATIVE_ACCURACY
        // Currently, we only use the BACKEND_GAMMA and BACKEND_INDEX_OFFSET
        private static DDSketch CreateSketch()
        {
            return new DDSketch(
                new LogarithmicMapping(1.015625, 1.8761281912861705), // Those are supposedly the values expected by the backend
                new CollapsingLowestDenseStore(2048),
                new CollapsingLowestDenseStore(2048));
        }
    }
}
