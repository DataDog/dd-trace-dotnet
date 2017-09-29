using OpenTracing;
using System;
using System.Collections.Generic;

namespace Datadog.Tracer
{
    public class Span : ISpan
    {
        private IDatadogTracer _tracer;
        private Dictionary<string, string> _tags;
        private bool isFinished;

        public ISpanContext Context { get; }

        internal DateTimeOffset StartTime { get; }

        internal DateTimeOffset EndTime { get; set; }

        internal string OperationName { get; set; }

        internal Span(IDatadogTracer tracer, SpanContext parent, DateTimeOffset? start)
        {
            _tracer = tracer;
            if(parent != null)
            {
                Context = new SpanContext(parent.TraceId, parent.SpanId);
            }
            else
            {
                Context = new SpanContext();
            }
            if (start.HasValue)
            {
                StartTime = start.Value;
            }
            else
            {
                StartTime = DateTimeOffset.UtcNow;
            }
        }

        public void Dispose()
        {
            Finish();
        }

        public void Finish()
        {
            Finish(DateTimeOffset.UtcNow);
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            if (!isFinished)
            {
                isFinished = true;
                EndTime = finishTimestamp;
                _tracer.Write(this);
            }
        }

        public string GetBaggageItem(string key)
        {
            throw new NotImplementedException();
        }

        public ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
        {
            throw new NotImplementedException();
        }

        public ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
        {
            throw new NotImplementedException();
        }

        public ISpan Log(string eventName)
        {
            throw new NotImplementedException();
        }

        public ISpan Log(DateTimeOffset timestamp, string eventName)
        {
            throw new NotImplementedException();
        }

        public ISpan SetBaggageItem(string key, string value)
        {
            throw new NotImplementedException();
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

        public ISpan SetTag(string key, string value)
        {
            if(_tags == null)
            {
                _tags = new Dictionary<string, string>();
            }
            _tags[key] = value;
            return this;
        }

        internal string GetTag(string key)
        {
            string s = null;
            _tags?.TryGetValue(key, out s);
            return s;
        }
    }
}
