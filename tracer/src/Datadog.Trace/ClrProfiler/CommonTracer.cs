// <copyright file="CommonTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
            return (int?)Tracer.Instance.InternalActiveScope?.Span.Context?.TraceContext?.SamplingPriority;
        }

        public void SetSamplingPriority(int? samplingPriority)
        {
            var traceContext = Tracer.Instance.InternalActiveScope?.Span.Context?.TraceContext;

            if (traceContext != null)
            {
                traceContext.SetSamplingPriority((SamplingPriority?)samplingPriority, notifyDistributedTracer: false);
            }
        }
    }
}
