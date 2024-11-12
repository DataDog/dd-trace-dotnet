// <copyright file="OpenTracingSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Globalization;
using OpenTracing.Tag;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingSpan : global::OpenTracing.ISpan
    {
        internal OpenTracingSpan(ISpan span)
        {
            Span = span;
            Context = new OpenTracingSpanContext(span.Context);
        }

        public OpenTracingSpanContext Context { get; }

        global::OpenTracing.ISpanContext global::OpenTracing.ISpan.Context => Context;

        internal ISpan Span { get; }

        // TODO lucas: inline this in a separate commit, it will modify a lot of files
        // This is only exposed for tests
        internal ISpan DDSpan => Span;

        internal string OperationName => Span.OperationName;

        public string GetBaggageItem(string key) => null;

        public global::OpenTracing.ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields) => this;

        public global::OpenTracing.ISpan Log(string eventName) => this;

        public global::OpenTracing.ISpan Log(DateTimeOffset timestamp, string eventName) => this;

        public global::OpenTracing.ISpan Log(IEnumerable<KeyValuePair<string, object>> fields) => this;

        public global::OpenTracing.ISpan SetBaggageItem(string key, string value) => this;

        public global::OpenTracing.ISpan SetOperationName(string operationName)
        {
            Span.OperationName = operationName;
            return this;
        }

        public string GetTag(string key)
        {
            return Span.GetTag(key);
        }

        public global::OpenTracing.ISpan SetTag(string key, bool value)
        {
            return SetTag(key, value.ToString());
        }

        public global::OpenTracing.ISpan SetTag(string key, double value)
        {
            return SetTag(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public global::OpenTracing.ISpan SetTag(string key, int value)
        {
            return SetTag(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public global::OpenTracing.ISpan SetTag(BooleanTag tag, bool value)
        {
            return SetTag(tag.Key, value);
        }

        public global::OpenTracing.ISpan SetTag(IntOrStringTag tag, string value)
        {
            return SetTag(tag.Key, value);
        }

        public global::OpenTracing.ISpan SetTag(IntTag tag, int value)
        {
            return SetTag(tag.Key, value);
        }

        public global::OpenTracing.ISpan SetTag(StringTag tag, string value)
        {
            return SetTag(tag.Key, value);
        }

        public global::OpenTracing.ISpan SetTag(string key, string value)
        {
            // TODO:bertrand do we want this behavior on the Span object too ?

            switch (key)
            {
                case DatadogTags.ResourceName:
                    Span.ResourceName = value;
                    return this;
                case DatadogTags.SpanType:
                    Span.Type = value;
                    return this;
                case DatadogTags.ServiceName:
                    Span.ServiceName = value;
                    return this;
                case DatadogTags.ServiceVersion:
                    Span.SetTag(Tags.Version, value);
                    break; // Continue to set the requested tag
            }

            if (key == global::OpenTracing.Tag.Tags.Error.Key)
            {
                Span.Error = value == bool.TrueString;
                return this;
            }

            Span.SetTag(key, value);
            return this;
        }

        public void Finish()
        {
            Span.Finish();
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            Span.Finish(finishTimestamp);
        }
    }
}
