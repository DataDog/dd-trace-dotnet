// <copyright file="RareSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.TraceSamplers
{
    internal class RareSampler : ITraceChunkSampler
    {
        private const int CacheLimit = 200; // Uses default value of 200 as found in the trace agent

        private readonly HashSet<StatsAggregationKey> _keys = new();
        private readonly Queue<StatsAggregationKey> _cache = new();

        public RareSampler(ImmutableTracerSettings settings)
        {
            IsEnabled = settings.IsRareSamplerEnabled;
        }

        public bool IsEnabled { get; }

        /// <summary>
        /// Samples the trace chunk with the following rules:
        /// 1) If the sampling priority is > 0, only update the seen spans and return false.
        /// 2) Iterate through the trace chunk and sample each span (see <see cref="SampleSpan(Span)"/> for the current logic). As soon as span is kept, stop iterating through the trace chunk.
        /// 3) If a span was kept, update the seen spans.
        /// 4) Return whether a span was sampled.
        /// </summary>
        /// <param name="traceChunk">The input trace chunk</param>
        /// <returns>true when a rare span is found, false otherwise</returns>
        public bool Sample(ArraySegment<Span> traceChunk)
        {
            if (!IsEnabled)
            {
                return false;
            }

            if (SamplingHelpers.IsKeptBySamplingPriority(traceChunk))
            {
                UpdateSeenSpans(traceChunk);
                return false;
            }

            return SampleSpansAndUpdateSeenSpansIfKept(traceChunk);
        }

        private void UpdateSeenSpans(ArraySegment<Span> trace)
        {
            for (int i = 0; i < trace.Count; i++)
            {
                var span = trace.Array![i + trace.Offset];
                if (span.IsTopLevel || span.GetMetric(Tags.Measured) == 1.0 || span.GetMetric(Tags.PartialSnapshot) > 0)
                {
                    UpdateSpan(span);
                }
            }
        }

        private bool SampleSpansAndUpdateSeenSpansIfKept(ArraySegment<Span> trace)
        {
            bool rareSpanFound = false;

            for (int i = 0; i < trace.Count; i++)
            {
                var span = trace.Array![i + trace.Offset];
                if (span.IsTopLevel || span.GetMetric(Tags.Measured) == 1.0 || span.GetMetric(Tags.PartialSnapshot) > 0)
                {
                    // Follow agent implementation to mark and exit on first sampled span
                    // Source: https://github.com/DataDog/datadog-agent/blob/bc4902fe62838b02e9ef7f2082d0cab6c24724fa/pkg/trace/sampler/rare_sampler.go#L106
                    rareSpanFound = SampleSpan(span);
                    if (rareSpanFound)
                    {
                        break;
                    }
                }
            }

            if (rareSpanFound)
            {
                UpdateSeenSpans(trace);
            }

            return rareSpanFound;
        }

        private bool SampleSpan(Span span)
        {
            var key = StatsAggregator.BuildKey(span);
            var isNewKey = _keys.Add(key);

            if (isNewKey)
            {
                span.Tags.SetMetric(Metrics.RareSpan, 1);
                UpdateCache(key);
            }

            return isNewKey;
        }

        private void UpdateSpan(Span span)
        {
            var key = StatsAggregator.BuildKey(span);
            var isNewKey = _keys.Add(key);

            if (isNewKey)
            {
                UpdateCache(key);
            }
        }

        private void UpdateCache(StatsAggregationKey key)
        {
            if (_cache.Count == CacheLimit)
            {
                var oldKey = _cache.Dequeue();
                _keys.Remove(oldKey);
            }

            _cache.Enqueue(key);
        }
    }
}
