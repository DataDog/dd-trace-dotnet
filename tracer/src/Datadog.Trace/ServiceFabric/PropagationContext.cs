// <copyright file="PropagationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ServiceFabric
{
    internal readonly struct PropagationContext
    {
        public readonly ulong TraceId;
        public readonly ulong ParentSpanId;
        public readonly SamplingPriority? SamplingPriority;
        public readonly string? Origin;

        public PropagationContext(ulong traceId, ulong parentSpanId, SamplingPriority? samplingPriority, string? origin)
        {
            TraceId = traceId;
            ParentSpanId = parentSpanId;
            SamplingPriority = samplingPriority;
            Origin = origin;
        }
    }
}
