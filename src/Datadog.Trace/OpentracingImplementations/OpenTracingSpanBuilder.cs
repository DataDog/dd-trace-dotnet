using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using OpenTracing;
using OpenTracing.Util;

namespace Datadog.Trace
{
    internal class OpenTracingSpanBuilder : ISpanBuilder
    {
        private static ILog _log = LogProvider.For<OpenTracingSpanBuilder>();

        private readonly Tracer _tracer;
        private readonly object _lock = new object();
        private readonly string _operationName;
        private SpanContext _parent;
        private DateTimeOffset? _start;
        private Dictionary<string, string> _tags;
        private string _serviceName;

        internal OpenTracingSpanBuilder(Tracer tracer, string operationName)
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

        public ISpanBuilder FollowsFrom(ISpan parent)
        {
            _log.Debug("ISpanBuilder.FollowsFrom is not implemented by Datadog.Trace");
            return this;
        }

        public ISpanBuilder FollowsFrom(ISpanContext parent)
        {
            _log.Debug("ISpanBuilder.FollowsFrom is not implemented by Datadog.Trace");
            return this;
        }

        public ISpanBuilder IgnoreActiveSpan()
        {
            _log.Debug("ISpanBuilder.FollowsFrom is not implemented by Datadog.Trace");
            return this;
        }

        public IScope StartActive(bool finishSpanOnDispose)
        {
            Scope scope = _tracer.StartActive(string.Empty, finishOnClose: finishSpanOnDispose);
            return new OpenTracingScope(scope);
        }

        public ISpan Start()
        {
            lock (_lock)
            {
                Scope scope = _tracer.StartActive(_operationName, _parent, _serviceName, _start);
                var span = new OpenTracingSpan(scope.Span);

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
            return WithTag(key, value.ToString());
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
