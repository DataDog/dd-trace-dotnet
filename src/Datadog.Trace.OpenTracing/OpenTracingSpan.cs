using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Logging;
using OpenTracing;

namespace Datadog.Trace
{
    public class OpenTracingSpan : ISpan
    {
        private static ILog _log = LogProvider.For<OpenTracingSpan>();

        public ISpanContext Context { get; }

        public Span DatadogSpan { get; }

        internal OpenTracingSpan(Span datadogSpan)
        {
            DatadogSpan = datadogSpan;
            Context = new OpenTracingSpanContext(DatadogSpan.Context);
        }

        public string GetBaggageItem(string key)
        {
            _log.Debug("ISpan.GetBaggageItem is not implemented by Datadog.Trace");
            return null;
        }

        public ISpan Log(DateTimeOffset timestamp, IDictionary<string, object> fields)
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

        public ISpan Log(IDictionary<string, object> fields)
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
            DatadogSpan.OperationName = operationName;
            return this;
        }

        public ISpan SetTag(string key, bool value)
        {
            return SetTag(key, value.ToString());
        }

        public ISpan SetTag(string key, double value)
        {
            return SetTag(key, value.ToString(CultureInfo.CurrentCulture));
        }

        public ISpan SetTag(string key, int value)
        {
            return SetTag(key, value.ToString());
        }

        public ISpan SetTag(string key, string value)
        {
            // TODO:bertrand do we want this behavior on the Span object too ?
            if (key == DDTags.ResourceName)
            {
                DatadogSpan.ResourceName = value;
                return this;
            }

            if (key == global::OpenTracing.Tag.Tags.Error.Key)
            {
                DatadogSpan.Error = value == bool.TrueString;
                return this;
            }

            if (key == DDTags.SpanType)
            {
                DatadogSpan.Type = value;
                return this;
            }

            DatadogSpan.SetTag(key, value);
            return this;
        }

        public void Finish()
        {
            DatadogSpan.Finish();
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            DatadogSpan.Finish(finishTimestamp);
        }
    }
}