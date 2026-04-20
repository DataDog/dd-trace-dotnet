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
        public StatsBucket(StatsAggregationKey key, List<byte[]> peerTags)
        {
            Key = key;
            OkSummary = CreateSketch();
            ErrorSummary = CreateSketch();
            PeerTags = peerTags;
        }

        public StatsAggregationKey Key { get; }

        // Based on https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/stats/weight.go
        // Hits, Errors, and TopLevelHits are doubles to accumulate fractional sampling weights (1/rate)
        public double Hits { get; set; }

        public double Errors { get; set; }

        // Duration is a double to accumulate fractional sampling weights (1/rate), like Hits/Errors/TopLevelHits.
        // Based on https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/stats/statsraw.go
        public double Duration { get; set; }

        public DDSketch OkSummary { get; }

        public DDSketch ErrorSummary { get; }

        public double TopLevelHits { get; set; }

        public List<byte[]> PeerTags { get; }

        public void Clear()
        {
            Hits = 0;
            Errors = 0;
            Duration = 0;
            TopLevelHits = 0;
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
