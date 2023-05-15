// <copyright file="AnalyticsEventsSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.TraceSamplers
{
    internal class AnalyticsEventsSampler : ITraceChunkSampler
    {
        public bool Sample(ArraySegment<Span> trace)
        {
            for (int i = 0; i < trace.Count; i++)
            {
                var span = trace.Array![i + trace.Offset];

                if (span.GetMetric(Tags.Analytics) is { } rate)
                {
                    return SamplingHelpers.SampleByRate(span.TraceId128, rate);
                }
            }

            return false;
        }
    }
}
