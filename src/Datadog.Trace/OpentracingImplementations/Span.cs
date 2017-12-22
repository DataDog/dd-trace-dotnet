using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using OpenTracing;

namespace Datadog.Trace
{
    // TODO:bertrand this class should not be public
    public class Span : ISpan
    {
        private static ILog _log = LogProvider.For<Span>();

        private SpanBase _span;

        internal Span(SpanBase span)
        {
            _span = span;
        }

        internal Span(IDatadogTracer tracer, SpanContext parent, string operationName, string serviceName, DateTimeOffset? start)
        {
            _span = new SpanBase(tracer, parent, operationName, serviceName, start);
        }

        public ISpanContext Context => _span.Context;

        // This is only exposed for tests
        internal SpanBase DDSpan => _span;

        public string GetBaggageItem(string key)
        {
            _log.Debug("ISpan.GetBaggageItem is not implemented by Datadog.Trace");
            return null;
        }

        public ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
        {
            _log.Debug("ISpan.Log is not implemented by Datadog.Trace");
            return this;
        }

        public ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
        {
            _log.Debug("ISpan.Log is not implemented by Datadog.Trace");
            return this;
        }

        public ISpan Log(string eventName)
        {
            _log.Debug("ISpan.Log is not implemented by Datadog.Trace");
            return this;
        }

        public ISpan Log(DateTimeOffset timestamp, string eventName)
        {
            _log.Debug("ISpan.Log is not implemented by Datadog.Trace");
            return this;
        }

        public ISpan SetBaggageItem(string key, string value)
        {
            _log.Debug("ISpan.SetBaggageItem is not implemented by Datadog.Trace");
            return this;
        }

        public ISpan SetOperationName(string operationName)
        {
            _span.OperationName = operationName;
            return this;
        }

        public ISpan SetTag(string key, bool value)
        {
            return SetTag(key, value.ToString());
        }

        public ISpan SetTag(string key, double value)
        {
            return SetTag(key, value.ToString());
        }

        public ISpan SetTag(string key, int value)
        {
            return SetTag(key, value.ToString());
        }

        public ISpan SetTag(string key, string value)
        {
            // TODO:bertrand do we want this behavior on the Span object too ?
            switch (key)
            {
                case DDTags.ResourceName:
                    _span.ResourceName = value;
                    return this;
                case Tags.Error:
                    _span.Error = value == "True";
                    return this;
                case DDTags.SpanType:
                    _span.Type = value;
                    return this;
            }

            _span.SetTag(key, value);
            return this;
        }

        public void Finish()
        {
            _span.Finish();
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            _span.Finish(finishTimestamp);
        }

        public void Dispose()
        {
            _span.Dispose();
        }
    }
}