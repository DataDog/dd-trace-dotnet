// <copyright file="CommonTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// This class contains methods implemented by both the automatic and manual tracer.
    /// It is used for duplex communication.
    /// </summary>
    internal abstract class CommonTracer : ICommonTracer2
    {
        // Not used anymore. Keep it for backwards compat with ICommonTracer.
        public int? GetSamplingPriority()
        {
            return Tracer.Instance.InternalActiveScope?.Span.Context?.TraceContext?.SamplingDecision?.Priority;
        }

        // Not used anymore. Keep it for backwards compat with ICommonTracer.
        public void SetSamplingPriority(int? samplingPriority)
        {
            if (samplingPriority is null)
            {
                ClearSamplingDecision();
            }
            else
            {
                SetSamplingDecision(samplingPriority.Value, SamplingMechanism.Unknown, rate: null);
            }
        }

        public bool GetSamplingDecision(out int priority, out int mechanism, out double? rate)
        {
            var samplingDecision = Tracer.Instance.InternalActiveScope?.Span.Context?.TraceContext?.SamplingDecision;

            if (samplingDecision != null)
            {
                (priority, mechanism, rate) = samplingDecision.Value;
                return true;
            }

            priority = default;
            mechanism = default;
            rate = default;
            return false;
        }

        public void SetSamplingDecision(int priority, int mechanism, double? rate)
        {
            var samplingDecision = new SamplingDecision(priority, mechanism, rate);
            var traceContext = Tracer.Instance.InternalActiveScope?.Span.Context?.TraceContext;

            traceContext?.SetSamplingDecision(samplingDecision, notifyDistributedTracer: false);
        }

        public void ClearSamplingDecision()
        {
            var traceContext = Tracer.Instance.InternalActiveScope?.Span.Context?.TraceContext;

            traceContext?.SetSamplingDecision(samplingDecision: null, notifyDistributedTracer: false);
        }
    }
}
