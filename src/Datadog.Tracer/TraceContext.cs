using System.Collections.Generic;
using System.Threading;

namespace Datadog.Tracer
{
    internal class TraceContext : ITraceContext
    {
        private object _lock = new object();
        private IDatadogTracer _tracer;
        private List<Span> _spans = new List<Span>();
        private int _openSpans = 0;
        private AsyncLocal<SpanContext> _currentSpanContext = new AsyncLocal<SpanContext>();

        public bool Sampled { get; set; }

        public TraceContext(IDatadogTracer tracer)
        {
            _tracer = tracer;
        }

        public SpanContext GetCurrentSpanContext()
        {
            return _currentSpanContext.Value;
        }

        public void AddSpan(Span span)
        {
            lock (_lock)
            {
                _currentSpanContext.Value = span.DatadogContext;
                _spans.Add(span);
                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            lock (_lock)
            {
                _currentSpanContext.Value = _currentSpanContext.Value?.Parent;
                _openSpans--;
                if (span.IsRootSpan)
                {
                    if (_openSpans != 0)
                    {
                        // TODO:bertrand log error and do not send anything
                        // Instead detect if we are being garbage collected and warn at that point
                    }
                    else
                    {
                        _tracer.Write(_spans);
                    }
                    // TODO:bertrand make sure the tracecontext is reset
                }
            }
        }
    }
}
