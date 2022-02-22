// <copyright file="CommonTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Sampling;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// This class contains methods implemented by both the automatic and manual tracer.
    /// It is used for duplex communication.
    /// </summary>
    internal abstract class CommonTracer : ICommonTracer
    {
        public int? GetSamplingPriority()
        {
            return Tracer.Instance.InternalActiveScope?.Span.Context?.TraceContext?.SamplingDecision?.Priority;
        }

        public void SetSamplingPriority(int? samplingPriority)
        {
            Tracer.Instance.InternalActiveScope?.Span.Context?.TraceContext
                 ?.SetSamplingDecision(samplingPriority, SamplingMechanism.Unknown, notifyDistributedTracer: false);
        }
    }
}
