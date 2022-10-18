// <copyright file="SamplingHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Util
{
    internal static class SamplingHelpers
    {
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        internal static bool SampleByRate(ulong id, double rate) =>
            ((id * KnuthFactor) % TracerConstants.MaxTraceId) <= (rate * TracerConstants.MaxTraceId);

        internal static bool IsKeptBySamplingPriority(ArraySegment<Span> trace) =>
            trace.Array![trace.Offset].Context.TraceContext?.SamplingPriority > 0;

        internal static int? GetDecisionMaker(TraceContext traceContext)
        {
            var decisionMaker = traceContext.Tags.GetTag(Tags.Propagated.DecisionMaker);

            if (decisionMaker != null)
            {
                var hyphen = decisionMaker.IndexOf('-');

                if (hyphen != -1 && int.TryParse(decisionMaker.Substring(hyphen + 1), out var value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
