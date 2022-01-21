// <copyright file="AutomaticTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.ClrProfiler
{
    internal class AutomaticTracer : CommonTracer, IAutomaticTracer2, IDistributedTracer
    {
        private static readonly AsyncLocal<IReadOnlyDictionary<string, string>> DistributedTrace = new();
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AutomaticTracer));

        private static string _runtimeId;

        private ICommonTracer _child;

        bool IDistributedTracer.IsChildTracer => false;

        IReadOnlyDictionary<string, string> IDistributedTracer.GetSpanContextRaw()
        {
            if (_child is null)
            {
                return null;
            }
            else
            {
                return DistributedTrace.Value;
            }
        }

        IScope IDistributedTracer.GetActiveScope()
        {
            // The automatic tracer doesn't need to get the manual active trace
            return null;
        }

        SpanContext IDistributedTracer.GetSpanContext()
        {
            if (_child is null)
            {
                return null;
            }

            var value = DistributedTrace.Value;

            if (value is SpanContext spanContext)
            {
                return spanContext;
            }

            return SpanContextPropagator.Instance.Extract(value);
        }

        void IDistributedTracer.SetSpanContext(IReadOnlyDictionary<string, string> value)
        {
            // This is a performance optimization. See comment in GetDistributedTrace() about potential race condition
            if (_child != null)
            {
                DistributedTrace.Value = value;
            }
        }

        SamplingDecision? IDistributedTracer.GetSamplingDecision()
        {
            if (_child is ICommonTracer2 commonTracer2)
            {
                if (commonTracer2.GetSamplingDecision(out var priority, out var mechanism, out var rate))
                {
                    return new SamplingDecision(priority, mechanism, rate);
                }

                return null;
            }

            // ICommonTracer2 is not available
            var samplingPriority = _child?.GetSamplingPriority();

            if (samplingPriority != null)
            {
                return new SamplingDecision(samplingPriority.Value, SamplingMechanism.Unknown);
            }

            return null;
        }

        void IDistributedTracer.SetSamplingDecision(SamplingDecision? samplingDecision)
        {
            if (_child is ICommonTracer2 commonTracer2)
            {
                if (samplingDecision is null)
                {
                    commonTracer2.ClearSamplingDecision();
                    return;
                }

                var (samplingPriority, samplingMechanism, rate) = samplingDecision.Value;
                commonTracer2.SetSamplingDecision(samplingPriority, samplingMechanism, rate);
                return;
            }

            // ICommonTracer2 is not available
            _child?.SetSamplingPriority(samplingDecision?.Priority);
        }

        string IDistributedTracer.GetRuntimeId() => GetAutomaticRuntimeId();

        public object GetAutomaticActiveScope()
        {
            return Tracer.Instance.InternalActiveScope;
        }

        /// <summary>
        /// Gets the internal distributed trace object
        /// </summary>
        /// <returns>Shared distributed trace object instance</returns>
        public IReadOnlyDictionary<string, string> GetDistributedTrace()
        {
            // There is a subtle race condition:
            // in a server application, the automated instrumentation can be loaded first (to process the incoming request)
            // In that case, IDistributedTracer.SetSpanContext will do nothing because the child tracer is not initialized yet.
            // Then manual instrumentation is loaded, and DistributedTrace.Value does not contain the parent trace.
            // To fix this, if DistributedTrace.Value is null, we also check if there's an active scope just in case.
            // This is a compromise: we add an additional asynclocal read for the manual tracer when there is no parent trace,
            // but it allows us to remove the asynclocal write for the automatic tracer when running without manual instrumentation.

            return DistributedTrace.Value ?? Tracer.Instance.InternalActiveScope?.Span?.Context;
        }

        /// <summary>
        /// Sets the internal distributed trace object
        /// </summary>
        /// <param name="value">Shared distributed trace object instance</param>
        public void SetDistributedTrace(IReadOnlyDictionary<string, string> value)
        {
            if (_child != null)
            {
                DistributedTrace.Value = value;
            }
        }

        public void Register(object manualTracer)
        {
            Log.Information("Registering {child} as child tracer", manualTracer.GetType());

            // try the newest interface first and fall back to older ones
            _child = manualTracer.DuckAs<ICommonTracer2>() ??
                     manualTracer.DuckAs<ICommonTracer>();
        }

        public string GetAutomaticRuntimeId() => LazyInitializer.EnsureInitialized(ref _runtimeId, () => Guid.NewGuid().ToString());
    }
}
