using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class TraceContext : ITraceContext
    {
        private static ILog _log = LogProvider.For<TraceContext>();

        private object _lock = new object();
        private IDatadogTracer _tracer;
        private List<Span> _spans = new List<Span>();
        private int _openSpans = 0;
        private DateTimeOffset _start;
        private Stopwatch _sw;

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

        public void AddSpan(Span span)
        {
            lock (_lock)
            {
                _spans.Add(span);
                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            lock (_lock)
            {
                _openSpans--;
                if (_openSpans == 0)
                {
                    _tracer.Write(_spans);
                }
            }
        }
    }
}
