// <copyright file="SamplingHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Util
{
    internal static class SamplingHelpers
    {
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        private const ulong MaxInt64 = long.MaxValue;

        internal static bool SampleByRate(TraceId traceId, double rate) =>
            // use the lower 64 bits of the trace id which are the only random part
            ((traceId.Lower * KnuthFactor) % MaxInt64) <= (rate * MaxInt64);

        internal static bool SampleByRate(ulong spanId, double rate) =>
            ((spanId * KnuthFactor) % MaxInt64) <= (rate * MaxInt64);

        internal static bool IsKeptBySamplingPriority(ArraySegment<Span> trace) =>
            trace.Array![trace.Offset].Context.TraceContext?.SamplingPriority > 0;
    }
}
