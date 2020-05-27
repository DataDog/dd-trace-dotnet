using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.Trace
{
    internal class TraceContext : ITraceContext
    {
        private static readonly ICoreLogger Log = CoreLogging.For<TraceContext>();
        private static Action<Span> _decorateRootSpan = null;

        private readonly DateTimeOffset _utcStart = DateTimeOffset.UtcNow;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly List<Span> _spans = new List<Span>();

        private readonly ITraceContextStrategy _traceContextStrategy = null;

        private int _openSpans;
        private SamplingPriority? _samplingPriority;
        private bool _samplingPriorityLocked;

        public TraceContext(ITraceContextStrategy traceContextStrategy)
        {
            _traceContextStrategy = traceContextStrategy;
        }

        public Span RootSpan { get; private set; }

        public DateTimeOffset UtcNow => _utcStart.Add(_stopwatch.Elapsed);

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
            lock (_spans)
            {
                if (RootSpan == null)
                {
                    // first span added is the root span
                    RootSpan = span;
                    DecorateRootSpan(span);

                    if (_samplingPriority == null)
                    {
                        if (span.Context.Parent is SpanContext context && context.SamplingPriority != null)
                        {
                            // this is a root span created from a propagated context that contains a sampling priority.
                            // lock sampling priority when a span is started from a propagated trace.
                            _samplingPriority = context.SamplingPriority;
                            LockSamplingPriority();
                        }
                        else
                        {
                            // this is a local root span (i.e. not propagated).
                            // determine an initial sampling priority for this trace, but don't lock it yet
                            _samplingPriority = _traceContextStrategy.GetSamplingPriority(RootSpan);
                        }
                    }
                }

                _spans.Add(span);
                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            if (span == RootSpan)
            {
                // lock sampling priority and set metric when root span finishes
                LockSamplingPriority();

                if (_samplingPriority == null)
                {
                    Log.Warning("Cannot set span metric for sampling priority before it has been set.");
                }
                else
                {
                    span.SetMetric(Metrics.SamplingPriority, (int)_samplingPriority);
                }
            }

            Span[] spansToWrite = null;

            lock (_spans)
            {
                _openSpans--;

                if (_openSpans == 0)
                {
                    spansToWrite = _spans.ToArray();
                    _spans.Clear();
                }
            }

            if (spansToWrite != null)
            {
                _traceContextStrategy.Write(spansToWrite);
            }
        }

        public void LockSamplingPriority()
        {
            if (_samplingPriority == null)
            {
                Log.Warning("Cannot lock sampling priority before it has been set.");
            }
            else
            {
                _samplingPriorityLocked = true;
            }
        }

        private static void DecorateRootSpan(Span span)
        {
            _decorateRootSpan?.Invoke(span);
        }
    }
}
