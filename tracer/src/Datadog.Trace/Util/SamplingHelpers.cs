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

        // This is intentionally not ulong.MaxValue.
        private const ulong MaxInt64 = long.MaxValue;

        /// <summary>
        /// Determines if a trace should be kept based on its trace id and the given sampling rate.
        /// </summary>
        /// <param name="traceId">The 128-bit id of the trace.</param>
        /// <param name="rate">The sampling rate to apply.</param>
        /// <returns><c>true</c> if the trace should be sampled (kept), <c>false</c> otherwise.</returns>
        internal static bool SampleByRate(TraceId traceId, double rate) =>
            // use the lower 64 bits of the trace id which are the only random part
            SampleByRate(traceId.Lower, rate);

        /// <summary>
        /// Determines if an object (such as a span) should be kept based on
        /// the object's 64-bit id and the given sampling rate.
        /// </summary>
        /// <param name="id">The 64-bit id of the object. For example, a span id.</param>
        /// <param name="rate">The sampling rate to apply.</param>
        /// <returns><c>true</c> if the object should be sampled (kept), <c>false</c> otherwise.</returns>
        internal static bool SampleByRate(ulong id, double rate) =>
            ((id * KnuthFactor) % MaxInt64) <= (rate * MaxInt64);

        internal static bool IsKeptBySamplingPriority(ArraySegment<Span> trace) =>
            trace.Array![trace.Offset].Context.TraceContext?.SamplingPriority > 0;
    }
}
