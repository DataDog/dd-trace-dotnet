// <copyright file="RareSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.TraceSamplers
{
    internal class RareSampler : ITraceSampler
    {
        private const string RareKey = "_dd.rare";
        private readonly HashSet<StatsAggregationKey> _keys = new();

        public bool Sample(ArraySegment<Span> trace)
        {
            return SamplingHelpers.IsKeptBySamplingPriority(trace) switch
            {
                true => HandlePriorityTrace(trace),
                false => HandleTrace(trace)
            };
        }

        private bool HandlePriorityTrace(ArraySegment<Span> trace)
        {
            for (int i = 0; i < trace.Count; i++)
            {
                var span = trace.Array[i + trace.Offset];
                if (span.IsTopLevel || span.GetMetric(Tags.Measured) == 1.0 || span.GetMetric(Tags.PartialSnapshot) > 0)
                {
                    // Do not return immediately, as we might have multiple spans in this chunk that are rare
                    var key = StatsAggregator.BuildKey(span);
                    _keys.Add(key);
                }
            }

            return false;
        }

        private bool HandleTrace(ArraySegment<Span> trace)
        {
            bool sampled = false;
            for (int i = 0; i < trace.Count; i++)
            {
                var span = trace.Array[i + trace.Offset];
                if (span.IsTopLevel || span.GetMetric(Tags.Measured) == 1.0 || span.GetMetric(Tags.PartialSnapshot) > 0)
                {
                    var key = StatsAggregator.BuildKey(span);
                    sampled = _keys.Add(key);

                    // Follow agent implementation to mark and exit on first sampled span
                    // Source: https://github.com/DataDog/datadog-agent/blob/bc4902fe62838b02e9ef7f2082d0cab6c24724fa/pkg/trace/sampler/rare_sampler.go#L106
                    if (sampled)
                    {
                        span.Tags.SetMetric(RareKey, 1);
                        break;
                    }
                }
            }

            if (sampled)
            {
                HandlePriorityTrace(trace);
            }

            return sampled;
        }
    }
}
