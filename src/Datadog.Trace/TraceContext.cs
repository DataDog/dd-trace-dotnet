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
        /// Gets or sets sampling priority set by user code.
        /// Once the sampling priority is locked, setting this is a no-op.
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
                    _rootSpan = span;
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
                SamplingPriority priority = LockSamplingPriority();
                span.SetMetric(Metrics.SamplingPriority, (int)priority);
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

        public SamplingPriority LockSamplingPriority()
        {
            if (_rootSpan == null)
            {
                throw new InvalidOperationException("Cannot lock sampling priority on an empty trace (no spans).");
            }

            if (_samplingPriority == null)
            {
                if (_samplingPriorityLocked)
                {
                    // this should never happen
                    throw new InvalidOperationException("Sampling priority was locked before it was set.");
                }

                // if the sampling priority hasn't been set yet, determine a value now before locking it
                string env = _rootSpan.GetTag(Tags.Env);
                _samplingPriority = Tracer.Sampler.GetSamplingPriority(_rootSpan.ServiceName, env, _rootSpan.Context.TraceId);
            }

            _samplingPriorityLocked = true;
            return _samplingPriority.Value;
        }
    }
}
