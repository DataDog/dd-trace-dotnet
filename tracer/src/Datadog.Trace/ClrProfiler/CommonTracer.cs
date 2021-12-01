// <copyright file="CommonTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// This class contains methods implemented by both the automatic and manual tracer.
    /// It is used for duplex communication.
    /// </summary>
    internal abstract class CommonTracer : ICommonTracer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CommonTracer));

        public void LockSamplingPriority()
        {
            var traceContext = Tracer.Instance.ActiveScope?.Span.Context?.TraceContext;

            if (traceContext != null)
            {
                traceContext.LockSamplingPriority(notifyDistributedTracer: false);
            }
        }

        public int? TrySetSamplingPriority(int? samplingPriority)
        {
            var traceContext = Tracer.Instance.ActiveScope?.Span.Context?.TraceContext;

            // If there is no trace context, when a new span is propagated the sampling priority will automatically be locked
            // because it will be considered as a distributed trace
            if (traceContext != null)
            {
                if (traceContext.IsSamplingPriorityLocked())
                {
                    var currentSamplingPriority = traceContext.SamplingPriority;

                    if (currentSamplingPriority != null)
                    {
                        return (int)currentSamplingPriority.Value;
                    }

                    Log.Warning("SamplingPriority is locked without value");
                }
                else
                {
                    traceContext.SetSamplingPriority((SamplingPriority?)samplingPriority, notifyDistributedTracer: false);
                }
            }

            return samplingPriority;
        }
    }
}
