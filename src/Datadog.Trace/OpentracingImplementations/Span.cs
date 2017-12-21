using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using OpenTracing;

namespace Datadog.Trace
{
    // TODO:bertrand this class should not be public
    public class Span : SpanBase, ISpan
    {
        private static ILog _log = LogProvider.For<Span>();

        internal Span(IDatadogTracer tracer, SpanContext parent, string operationName, string serviceName, DateTimeOffset? start)
            : base(tracer, parent, operationName, serviceName, start)
        {
        }

        ISpanContext ISpan.Context => Context;

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
            OperationName = operationName;
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

        public new ISpan SetTag(string key, string value)
        {
            switch (key)
            {
                case DDTags.ResourceName:
                    ResourceName = value;
                    return this;
                case OpenTracing.Tags.Error:
                    Error = value == "True";
                    return this;
                case DDTags.SpanType:
                    Type = value;
                    return this;
            }

            base.SetTag(key, value);
            return this;
        }

        public new void Finish()
        {
            base.Finish();
        }

        public new void Finish(DateTimeOffset finishTimestamp)
        {
            base.Finish(finishTimestamp);
        }
    }
}