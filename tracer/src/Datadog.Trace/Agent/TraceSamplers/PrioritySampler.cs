// <copyright file="PrioritySampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Agent.TraceSamplers
{
    internal class PrioritySampler : ITraceSampler
    {
        public bool Sample(ArraySegment<Span> trace)
        {
            return trace.Array[trace.Offset].Context.TraceContext.SamplingPriority is int p && p > 0;
        }
    }
}
