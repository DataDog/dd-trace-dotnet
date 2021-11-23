// <copyright file="AutomaticTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler
{
    internal class AutomaticTracer : CommonTracer, IAutomaticTracer, IDistributedTracer
    {
        private static readonly AsyncLocal<IReadOnlyDictionary<string, string>> DistributedTrace = new();
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AutomaticTracer));

        private ICommonTracer _child;

        SpanContext IDistributedTracer.GetSpanContext()
        {
            return _child != null ? SpanContextPropagator.Instance.Extract(DistributedTrace.Value) : null;
        }

        void IDistributedTracer.SetSpanContext(SpanContext value)
        {
            // Locally setting the SpanContext, no need to do anything
        }

        void IDistributedTracer.LockSamplingPriority()
        {
            _child?.LockSamplingPriority();
        }

        SamplingPriority? IDistributedTracer.TrySetSamplingPriority(SamplingPriority? samplingPriority)
        {
            if (_child == null)
            {
                return samplingPriority;
            }

            return (SamplingPriority?)_child.TrySetSamplingPriority((int?)samplingPriority);
        }

        /// <summary>
        /// Gets the internal distributed trace object
        /// </summary>
        /// <returns>Shared distributed trace object instance</returns>
        public object GetDistributedTrace()
        {
            return Tracer.Instance.ActiveScope?.Span.Context;
        }

        /// <summary>
        /// Sets the internal distributed trace object
        /// </summary>
        /// <param name="value">Shared distributed trace object instance</param>
        public void SetDistributedTrace(object value)
        {
            DistributedTrace.Value = value as IReadOnlyDictionary<string, string>;
        }

        public void Register(object manualTracer)
        {
            Log.Information("Registering {child} as child tracer", manualTracer.GetType());
            _child = manualTracer.DuckCast<ICommonTracer>();
        }
    }
}
