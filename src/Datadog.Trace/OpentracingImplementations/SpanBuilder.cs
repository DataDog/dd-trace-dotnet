using System;
using Datadog.Trace.Logging;
using OpenTracing;

namespace Datadog.Trace
{
    internal class SpanBuilder : SpanBuilderBase<Span>, ISpanBuilder
    {
        private static ILog _log = LogProvider.For<SpanBuilder>();

        internal SpanBuilder(IDatadogTracer tracer, string operationName)
            : base(tracer, operationName)
        {
        }

        public ISpanBuilder AddReference(string referenceType, ISpanContext referencedContext)
        {
            if (referenceType == References.ChildOf)
            {
                AsChildOf(referencedContext as SpanContext);
                return this;
            }

            _log.Debug("ISpanBuilder.AddReference is not implemented for other references than ChildOf by Datadog.Trace");
            return this;
        }

        public ISpanBuilder AsChildOf(ISpan parent)
        {
            AsChildOf(parent.Context as SpanContext);
            return this;
        }

        public ISpanBuilder AsChildOf(ISpanContext parent)
        {
            base.AsChildOf(parent as SpanContext);
            return this;
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

        public new ISpan Start()
        {
            return base.Start();
        }

        public new ISpanBuilder WithStartTimestamp(DateTimeOffset startTimestamp)
        {
            base.WithStartTimestamp(startTimestamp);
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

        public new ISpanBuilder WithTag(string key, string value)
        {
            if (key == DDTags.ServiceName)
            {
                WithServiceName(value);
                return this;
            }

            base.WithTag(key, value);
            return this;
        }

        protected override Span NewSpan(IDatadogTracer tracer, SpanContext parent, string operationName, string serviceName, DateTimeOffset? startTime)
        {
            return new Span(tracer, parent, operationName, serviceName, startTime);
        }
    }
}