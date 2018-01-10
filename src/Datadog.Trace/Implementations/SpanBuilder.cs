using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class SpanBuilder
    {
        private static ILog _log = LogProvider.For<SpanBuilder>();

        private object _lock = new object();
        private IDatadogTracer _tracer;
        private string _operationName;
        private SpanContext _parent;
        private DateTimeOffset? _start;
        private Dictionary<string, string> _tags;
        private string _serviceName;

        internal SpanBuilder(IDatadogTracer tracer, string operationName)
        {
            _tracer = tracer;
            _operationName = operationName;
        }

        public SpanBuilder AsChildOf(SpanContext parent)
        {
            lock (_lock)
            {
                _parent = parent;
                return this;
            }
        }

        public Span Start()
        {
            lock (_lock)
            {
                var span = new Span(_tracer, _parent, _operationName, _serviceName, _start);
                span.TraceContext.AddSpan(span);
                if (_tags != null)
                {
                    foreach (var pair in _tags)
                    {
                        span.SetTag(pair.Key, pair.Value);
                    }
                }

                return span;
            }
        }

        public SpanBuilder WithTag(string key, string value)
        {
            lock (_lock)
            {
                if (_tags == null)
                {
                    _tags = new Dictionary<string, string>();
                }

                _tags[key] = value;
                return this;
            }
        }

        public SpanBuilder WithServiceName(string serviceName)
        {
            lock (_lock)
            {
                _serviceName = serviceName;
                return this;
            }
        }

        public SpanBuilder WithStartTimestamp(DateTimeOffset startTimestamp)
        {
            lock (_lock)
            {
                _start = startTimestamp;
                return this;
            }
        }
    }
}
