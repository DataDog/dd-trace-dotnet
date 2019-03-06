using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class TraceContext : ITraceContext
    {
        private static readonly ILog Log = LogProvider.For<TraceContext>();

        private readonly object _lock = new object();
        private readonly IDatadogTracer _tracer;
        private readonly List<Span> _spans = new List<Span>();

        private int _openSpans;

        public TraceContext(IDatadogTracer tracer)
        {
            _tracer = tracer;
        }

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

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
