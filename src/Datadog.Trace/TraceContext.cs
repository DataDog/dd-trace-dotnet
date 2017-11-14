using Datadog.Trace.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Datadog.Trace
{
    internal class TraceContext : ITraceContext
    {
        private static ILog _log = LogProvider.For<TraceContext>();

        private object _lock = new object();
        private IDatadogTracer _tracer;
        private List<Span> _spans = new List<Span>();
        private int _openSpans = 0;
        private AsyncLocalCompat<SpanContext> _currentSpanContext = new AsyncLocalCompat<SpanContext>("Datadog.Trace.TraceContext._currentSpanContext");
        private DateTimeOffset _start;
        private Stopwatch _sw;

        public bool Sampled { get; set; }

        public TraceContext(IDatadogTracer tracer)
        {
            _tracer = tracer;
            _start = DateTimeOffset.UtcNow;
            _sw = Stopwatch.StartNew();
        }

        public DateTimeOffset UtcNow()
        {
            return _start.Add(_sw.Elapsed);
        }

        public string DefaultServiceName => _tracer.DefaultServiceName;

        public SpanContext GetCurrentSpanContext()
        {
            return _currentSpanContext.Get();
        }

        public void AddSpan(Span span)
        {
            lock (_lock)
            {
                _currentSpanContext.Set(span.Context);
                _spans.Add(span);
                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            lock (_lock)
            {
                _currentSpanContext.Set(_currentSpanContext.Get()?.Parent);
                _openSpans--;
                if (span.IsRootSpan)
                {
                    _tracer.CloseCurrentTraceContext();
                    if (_openSpans != 0)
                    {
                        _log.DebugFormat("Some child spans were not finished before the root. {NumberOfOpenSpans}", _openSpans);
                        if (_tracer.IsDebugEnabled)
                        {
                            foreach (var s in _spans.Where(x => !x.IsFinished))
                            {
                                _log.DebugFormat("Span {UnfinishedSpan} was not finished before its root span", s);
                            }
                            // TODO:bertrand Instead detect if we are being garbage collected and warn at that point
                        }
                    }
                    else
                    {
                        _tracer.Write(_spans);
                    }
                }
            }
        }
    }
}
