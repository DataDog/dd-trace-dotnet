using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class TraceContext : ITraceContext
    {
        private static readonly ILog Log = LogProvider.For<TraceContext>();

        private readonly object _lock = new object();
        private readonly IDatadogTracer _tracer;
        private readonly List<Span> _spans = new List<Span>();
        private readonly DateTimeOffset _start;
        private readonly Stopwatch _sw;

        private int _openSpans = 0;

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
