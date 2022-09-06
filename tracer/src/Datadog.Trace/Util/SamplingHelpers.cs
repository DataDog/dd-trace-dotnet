// <copyright file="SamplingHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Util
{
    internal class SamplingHelpers
    {
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        internal static bool SampleByRate(ulong id, double rate) =>
            ((id * KnuthFactor) % TracerConstants.MaxTraceId) <= (rate * TracerConstants.MaxTraceId);

        internal static bool IsKeptBySamplingPriority(ArraySegment<Span> trace) =>
            trace.Array[trace.Offset].Context.TraceContext?.SamplingPriority is int p && p > 0;
    }
}
