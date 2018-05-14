using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using OpenTracing;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingSpan : ISpan
    {
        private static ILog _log = LogProvider.For<OpenTracingSpan>();

        private Scope _scope;

        internal OpenTracingSpan(Scope scope)
        {
            _scope = scope;
        }

        public ISpanContext Context => new OpenTracingSpanContext(_scope.Span.Context);

        // This is only exposed for tests
        internal Span DDSpan => _scope.Span;

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
            _scope.Span.OperationName = operationName;
            return this;
        }

        public string GetTag(string key)
        {
            return Span.GetTag(key);
        }

        public ISpan SetTag(string key, bool value)
        {
            return SetTag(key, value.ToString());
        }

        public ISpan SetTag(string key, double value)
        {
            return SetTag(key, value.ToString(CultureInfo.InvariantCulture));
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
                case DatadogTags.ResourceName:
                    _scope.Span.ResourceName = value;
                    return this;
                case global::OpenTracing.Tags.Error:
                    _scope.Span.Error = value == "True";
                    return this;
                case DatadogTags.SpanType:
                    _scope.Span.Type = value;
                    return this;
            }

            _scope.Span.SetTag(key, value);
            return this;
        }

        public void Finish()
        {
            _scope.Dispose();
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            _scope.Span.Finish(finishTimestamp);
            _scope.Dispose();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}