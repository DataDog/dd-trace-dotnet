// <copyright file="StatsBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.Datadog.Sketches.Mappings;
using Datadog.Trace.Vendors.Datadog.Sketches.Stores;

namespace Datadog.Trace.Agent
{
    internal class StatsBucket
    {
        public StatsBucket(StatsAggregationKey key)
        {
            Key = key;
            OkSummary = CreateSketch();
            ErrorSummary = CreateSketch();
        }

        public StatsAggregationKey Key { get; }

        public long Hits { get; set; }

        public long Errors { get; set; }

        public long Duration { get; set; }

        public DDSketch OkSummary { get; }

        public DDSketch ErrorSummary { get; }

        public long TopLevelHits { get; set; }

        public void Clear()
        {
            Hits = 0;
            Errors = 0;
            Duration = 0;
            TopLevelHits = 0;
            OkSummary.Clear();
            ErrorSummary.Clear();
        }

        private static DDSketch CreateSketch()
        {
            return new DDSketch(
                new LogarithmicMapping(1.015625, 1.8761281912861705), // Those are supposedly the values expected by the backend
                new CollapsingLowestDenseStore(2048),
                new CollapsingLowestDenseStore(2048));
        }
    }
}
