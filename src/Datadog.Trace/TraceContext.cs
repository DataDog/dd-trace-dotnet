using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class TraceContext : ITraceContext
    {
        private static readonly ILog Log = LogProvider.For<TraceContext>();

        private readonly object _lock = new object();
        private readonly List<Span> _spans = new List<Span>();

        private Span _rootSpan;
        private int _openSpans;
        private SamplingPriority? _samplingPriority;
        private bool _samplingPriorityLocked;

        public TraceContext(IDatadogTracer tracer)
        {
            Tracer = tracer;
        }

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public IDatadogTracer Tracer { get; }

        /// <summary>
        /// Gets or sets sampling priority.
        /// Once the sampling priority is locked with <see cref="LockSamplingPriority"/>,
        /// further attempts to set this are ignored.
        /// </summary>
        public SamplingPriority? SamplingPriority
        {
            get => _samplingPriority;
            set
            {
                if (!_samplingPriorityLocked)
                {
                    _samplingPriority = value;
                }
            }
        }

        public void AddSpan(Span span)
        {
            lock (_lock)
            {
                if (_openSpans == 0)
                {
                    // first span is the root span
                    _rootSpan = span;

                    if (_samplingPriority == null)
                    {
                    // determine an initial sampling priority for this trace, but don't lock it yet
                    string env = _rootSpan.GetTag(Tags.Env);
                    _samplingPriority = Tracer.Sampler.GetSamplingPriority(_rootSpan.ServiceName, env, _rootSpan.Context.TraceId);
                    }
                }

                _spans.Add(span);
                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            if (span == _rootSpan)
            {
                // lock sampling priority and set metric when root span finishes
                LockSamplingPriority();
                span.SetMetric(Metrics.SamplingPriority, (int)_samplingPriority);
            }

            lock (_lock)
            {
                _openSpans--;

                if (_openSpans == 0)
                {
                    Tracer.Write(_spans);
                }
            }
        }

        public void LockSamplingPriority()
        {
            if (_rootSpan == null)
            {
                throw new InvalidOperationException("Cannot lock sampling priority on an empty trace (no spans).");
            }

            if (_samplingPriorityLocked && _samplingPriority == null)
            {
                // this should never happen
                throw new InvalidOperationException("Sampling priority was locked before it was set.");
            }

            _samplingPriorityLocked = true;
        }
    }
}
