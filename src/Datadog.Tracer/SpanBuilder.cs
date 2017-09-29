using System;
using OpenTracing;
using System.Collections.Generic;

namespace Datadog.Tracer
{
    public class SpanBuilder : ISpanBuilder
    {
        private IDatadogTracer _tracer;
        private SpanContext _parent;
        private DateTimeOffset? _start;
        private Dictionary<string, string> _tags;

        internal SpanBuilder(IDatadogTracer tracer)
        {
            _tracer = tracer;
        }

        public ISpanBuilder AddReference(string referenceType, ISpanContext referencedContext)
        {
            if (referenceType == References.ChildOf)
            {
                _parent = referencedContext as SpanContext;
                return this;
            }
            throw new NotImplementedException();
        }

        public ISpanBuilder AsChildOf(ISpan parent)
        {
            _parent = parent.Context as SpanContext;
            return this;
        }

        public ISpanBuilder AsChildOf(ISpanContext parent)
        {
            _parent = parent as SpanContext;
            return this;
        }

        public ISpanBuilder FollowsFrom(ISpan parent)
        {
            throw new NotImplementedException();
        }

        public ISpanBuilder FollowsFrom(ISpanContext parent)
        {
            throw new NotImplementedException();
        }

        public ISpan Start()
        {
            var span = new Span(_tracer, _parent, _start);
            if(_tags != null)
            {
                foreach(var pair in _tags)
                {
                    span.SetTag(pair.Key, pair.Value);
                }
            }
            return span;
        }

        public ISpanBuilder WithStartTimestamp(DateTimeOffset startTimestamp)
        {
            _start = startTimestamp;
            return this;
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
            if(_tags == null)
            {
                _tags = new Dictionary<string, string>();
            }
            _tags[key] = value;
            return this;
        }
    }
}
