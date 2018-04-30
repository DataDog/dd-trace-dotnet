using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Logging;
using OpenTracing;

namespace Datadog.Trace
{
    internal class OpenTracingSpanBuilder : ISpanBuilder
    {
        private static ILog _log = LogProvider.For<OpenTracingSpanBuilder>();

        private readonly IDatadogTracer _tracer;
        private readonly object _lock = new object();
        private readonly string _operationName;
        private SpanContext _parent;
        private DateTimeOffset? _start;
        private Dictionary<string, string> _tags;
        private string _serviceName;
        private bool _ignoreActiveSpan;

        internal OpenTracingSpanBuilder(IDatadogTracer tracer, string operationName)
        {
            _tracer = tracer;
            _operationName = operationName;
        }

        public ISpanBuilder AddReference(string referenceType, ISpanContext referencedContext)
        {
            lock (_lock)
            {
                if (referenceType == References.ChildOf)
                {
                    _parent = referencedContext as SpanContext;
                    return this;
                }
            }

            _log.Debug("ISpanBuilder.AddReference is not implemented for other references than ChildOf by Datadog.Trace");
            return this;
        }

        public ISpanBuilder AsChildOf(ISpan parent)
        {
            lock (_lock)
            {
                _parent = parent.Context as SpanContext;
                return this;
            }
        }

        public ISpanBuilder AsChildOf(ISpanContext parent)
        {
            lock (_lock)
            {
                _parent = parent as SpanContext;
                return this;
            }
        }

        public ISpanBuilder IgnoreActiveSpan()
        {
            _ignoreActiveSpan = true;
            return this;
        }

        public IScope StartActive(bool finishSpanOnDispose)
        {
            Scope ddScope = _tracer.StartActive(_operationName, _parent, _serviceName, _start, _ignoreActiveSpan, finishSpanOnDispose);
            return new OpenTracingScope(ddScope);
        }

        public ISpan Start()
        {
            lock (_lock)
            {
                Span ddSpan = _tracer.StartSpan(_operationName, _parent, _serviceName, _start);
                var otSpan = new OpenTracingSpan(ddSpan);

                if (_tags != null)
                {
                    foreach (var pair in _tags)
                    {
                        otSpan.SetTag(pair.Key, pair.Value);
                    }
                }

                return otSpan;
            }
        }

        public ISpanBuilder WithStartTimestamp(DateTimeOffset startTimestamp)
        {
            lock (_lock)
            {
                _start = startTimestamp;
                return this;
            }
        }

        public ISpanBuilder WithTag(string key, bool value)
        {
            return WithTag(key, value.ToString());
        }

        public ISpanBuilder WithTag(string key, double value)
        {
            return WithTag(key, value.ToString(CultureInfo.CurrentCulture));
        }

        public ISpanBuilder WithTag(string key, int value)
        {
            return WithTag(key, value.ToString());
        }

        public ISpanBuilder WithTag(string key, string value)
        {
            lock (_lock)
            {
                if (key == DDTags.ServiceName)
                {
                    _serviceName = value;
                    return this;
                }

                if (_tags == null)
                {
                    _tags = new Dictionary<string, string>();
                }

                _tags[key] = value;
                return this;
            }
        }
    }
}
