// <copyright file="OpenTracingSpanBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Globalization;
using OpenTracing;
using OpenTracing.Tag;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingSpanBuilder : ISpanBuilder
    {
        private readonly OpenTracingTracer _tracer;
        private readonly object _lock = new object();
        private readonly string _operationName;
        private global::OpenTracing.ISpanContext _parent;
        private DateTimeOffset? _start;
        private Dictionary<string, string> _tags;
        private string _serviceName;
        private bool _ignoreActiveSpan;

        internal OpenTracingSpanBuilder(OpenTracingTracer tracer, string operationName)
        {
            _tracer = tracer;
            _operationName = operationName;
        }

        public ISpanBuilder AddReference(string referenceType, global::OpenTracing.ISpanContext referencedContext)
        {
            lock (_lock)
            {
                if (referenceType == References.ChildOf)
                {
                    _parent = referencedContext;
                    return this;
                }
            }

            return this;
        }

        public ISpanBuilder AsChildOf(global::OpenTracing.ISpan parent)
        {
            lock (_lock)
            {
                _parent = parent.Context;
                return this;
            }
        }

        public ISpanBuilder AsChildOf(global::OpenTracing.ISpanContext parent)
        {
            lock (_lock)
            {
                _parent = parent;
                return this;
            }
        }

        public ISpanBuilder IgnoreActiveSpan()
        {
            _ignoreActiveSpan = true;
            return this;
        }

        public global::OpenTracing.ISpan Start()
        {
            lock (_lock)
            {
                ISpanContext parentContext = GetParentContext();
                ISpan ddSpan = _tracer.DatadogTracer.StartSpan(_operationName, parentContext, _serviceName, _start, _ignoreActiveSpan);
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

        public global::OpenTracing.IScope StartActive()
        {
            return StartActive(finishSpanOnDispose: true);
        }

        public global::OpenTracing.IScope StartActive(bool finishSpanOnDispose)
        {
            var span = Start();
            return _tracer.ScopeManager.Activate(span, finishSpanOnDispose);
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
            return WithTag(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public ISpanBuilder WithTag(string key, int value)
        {
            return WithTag(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public ISpanBuilder WithTag(BooleanTag tag, bool value)
        {
            return WithTag(tag.Key, value);
        }

        public ISpanBuilder WithTag(IntOrStringTag tag, string value)
        {
            return WithTag(tag.Key, value);
        }

        public ISpanBuilder WithTag(IntTag tag, int value)
        {
            return WithTag(tag.Key, value.ToString(CultureInfo.InvariantCulture));
        }

        public ISpanBuilder WithTag(StringTag tag, string value)
        {
            return WithTag(tag.Key, value);
        }

        public ISpanBuilder WithTag(string key, string value)
        {
            lock (_lock)
            {
                if (key == DatadogTags.ServiceName)
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

        private ISpanContext GetParentContext()
        {
            var openTracingSpanContext = _parent as OpenTracingSpanContext;
            var parentContext = openTracingSpanContext?.Context;

            if (parentContext == null && !_ignoreActiveSpan)
            {
                // if parent was not set explicitly, default to active span as parent (unless disabled)
                return _tracer.ActiveSpan?.Span.Context;
            }

            return parentContext;
        }
    }
}
