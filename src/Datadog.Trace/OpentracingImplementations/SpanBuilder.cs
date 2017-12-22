using System;
using Datadog.Trace.Logging;
using OpenTracing;

namespace Datadog.Trace
{
    internal class SpanBuilder : ISpanBuilder
    {
        private static ILog _log = LogProvider.For<SpanBuilder>();

        private SpanBuilderBase _spanBuilder;

        internal SpanBuilder(IDatadogTracer tracer, string operationName)
        {
           _spanBuilder = new SpanBuilderBase(tracer, operationName);
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
            _spanBuilder.AsChildOf(parent as SpanContext);
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

        public ISpan Start()
        {
            return new Span(_spanBuilder.Start());
        }

        public ISpanBuilder WithStartTimestamp(DateTimeOffset startTimestamp)
        {
            _spanBuilder.WithStartTimestamp(startTimestamp);
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
            if (key == DDTags.ServiceName)
            {
                _spanBuilder.WithServiceName(value);
                return this;
            }

            _spanBuilder.WithTag(key, value);
            return this;
        }
    }
}