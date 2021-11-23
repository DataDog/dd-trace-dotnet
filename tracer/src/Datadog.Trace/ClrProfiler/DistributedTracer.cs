// <copyright file="DistributedTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.ClrProfiler
{
    internal interface IDistributedTracer
    {
        SpanContext GetSpanContext();

        void SetSpanContext(SpanContext value);

        void LockSamplingPriority();

        SamplingPriority? TrySetSamplingPriority(SamplingPriority? samplingPriority);
    }

    internal interface ICommonTracer
    {
        void LockSamplingPriority();

        int? TrySetSamplingPriority(int? samplingPriority);
    }

    internal interface IAutomaticTracer : ICommonTracer
    {
        object GetDistributedTrace();

        void SetDistributedTrace(object trace);

        void Register(object manualTracer);
    }

    internal class AutomaticTracer : IAutomaticTracer, IDistributedTracer
    {
        private static readonly AsyncLocal<IReadOnlyDictionary<string, string>> DistributedTrace = new();
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AutomaticTracer));

        private ICommonTracer _child;

        private bool _multipleTracers;

        SpanContext IDistributedTracer.GetSpanContext()
        {
            if (_multipleTracers)
            {
                return SpanContextPropagator.Instance.Extract(DistributedTrace.Value);
            }

            return null;
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
            _multipleTracers = true;
            DistributedTrace.Value = value as IReadOnlyDictionary<string, string>;
        }

        public void LockSamplingPriority()
        {
            var traceContext = Tracer.Instance.ActiveScope?.Span.Context?.TraceContext;

            // If there is no trace context, when a new span is propagated the sampling priority will automatically be locked
            // because it will be considered as a distributed trace
            if (traceContext != null)
            {
                traceContext.LockSamplingPriority();
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
                    traceContext.SamplingPriority = (SamplingPriority)samplingPriority;
                }
            }

            return samplingPriority;
        }

        void IAutomaticTracer.Register(object manualTracer)
        {
            _child = manualTracer.DuckAs<ICommonTracer>();
        }
    }

    internal class ManualTracer : IDistributedTracer
    {
        private readonly IAutomaticTracer _parent;

        internal ManualTracer(IAutomaticTracer parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _parent.Register(this);
        }

        public SpanContext GetSpanContext()
        {
            var values = _parent.GetDistributedTrace();

            return SpanContextPropagator.Instance.Extract(values as IReadOnlyDictionary<string, string>);
        }

        public void SetSpanContext(SpanContext value)
        {
            _parent.SetDistributedTrace(value);
        }

        public void LockSamplingPriority()
        {
            _parent.LockSamplingPriority();
        }

        public SamplingPriority? TrySetSamplingPriority(SamplingPriority? samplingPriority)
        {
            return (SamplingPriority?)_parent.TrySetSamplingPriority((int?)samplingPriority);
        }
    }

    /// <summary>
    /// Used to distribute traces across multiple versions of the tracer
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DistributedTracer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DistributedTracer));

        static DistributedTracer()
        {
            var parent = GetDistributedTracer();

            if (parent == null)
            {
                Log.Information("Building automatic tracer");
                Instance = new AutomaticTracer();
            }
            else
            {
                try
                {
                    var parentTracer = parent.DuckCast<IAutomaticTracer>();

                    Log.Information("Building manual tracer, connected to " + parent.GetType().Assembly.ToString());

                    Instance = new ManualTracer(parentTracer);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while building the manual tracer, fallbacking to automatic");
                    Instance = new AutomaticTracer();
                }
            }
        }

        internal static IDistributedTracer Instance { get; }

        /// <summary>
        /// Get the instance of IDistributedTracer. This method will be rewritten by the profiler.
        /// </summary>
        /// <returns>The instance of IDistributedTracer</returns>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static object GetDistributedTracer() => Instance;
    }
}
