using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class SpanBuilderBase
    {
        private static ILog _log = LogProvider.For<SpanBuilderBase>();

        private object _lock = new object();
        private IDatadogTracer _tracer;
        private string _operationName;
        private SpanContext _parent;
        private DateTimeOffset? _start;
        private Dictionary<string, string> _tags;
        private string _serviceName;

        internal SpanBuilderBase(IDatadogTracer tracer, string operationName)
        {
            _tracer = tracer;
            _operationName = operationName;
        }

        public SpanBuilderBase AsChildOf(SpanContext parent)
        {
            lock (_lock)
            {
                _parent = parent;
                return this;
            }
        }

        public SpanBase Start()
        {
            lock (_lock)
            {
                var span = new SpanBase(_tracer, _parent, _operationName, _serviceName, _start);
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

        public SpanBuilderBase WithTag(string key, string value)
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

        public SpanBuilderBase WithServiceName(string serviceName)
        {
            lock (_lock)
            {
                _serviceName = serviceName;
                return this;
            }
        }

        public SpanBuilderBase WithStartTimestamp(DateTimeOffset startTimestamp)
        {
            lock (_lock)
            {
                _start = startTimestamp;
                return this;
            }
        }
    }
}
