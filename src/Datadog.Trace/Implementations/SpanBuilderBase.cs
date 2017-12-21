using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal abstract class SpanBuilderBase<TSpan>
        where TSpan : Span
    {
        private static ILog _log = LogProvider.For<SpanBuilderBase<TSpan>>();

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

        protected abstract TSpan NewSpan(IDatadogTracer tracer, SpanContext parent, string operationName, string serviceName, DateTimeOffset? startTime);

        protected SpanBuilderBase<TSpan> AsChildOf(SpanContext parent)
        {
            lock (_lock)
            {
                _parent = parent;
                return this;
            }
        }

        protected TSpan Start()
        {
            lock (_lock)
            {
                var span = NewSpan(_tracer, _parent, _operationName, _serviceName, _start);
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

        protected SpanBuilderBase<TSpan> WithTag(string key, string value)
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

        protected SpanBuilderBase<TSpan> WithServiceName(string serviceName)
        {
            lock (_lock)
            {
                _serviceName = serviceName;
                return this;
            }
        }

        protected SpanBuilderBase<TSpan> WithStartTimestamp(DateTimeOffset startTimestamp)
        {
            lock (_lock)
            {
                _start = startTimestamp;
                return this;
            }
        }
    }
}
