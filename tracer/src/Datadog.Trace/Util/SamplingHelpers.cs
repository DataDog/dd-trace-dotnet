// <copyright file="SamplingHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Util
{
    internal class SamplingHelpers
    {
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        internal static bool SampleByRate(ulong traceId, double rate) =>
            ((traceId * KnuthFactor) % TracerConstants.MaxTraceId) <= (rate * TracerConstants.MaxTraceId);
    }
}
