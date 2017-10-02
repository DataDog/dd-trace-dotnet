using OpenTracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.Tracer
{
    // TODO:bertrand, should a Span be thread safe?
    public class Span : ISpan
    {
        private IDatadogTracer _tracer;
        private Dictionary<string, string> _tags;
        private bool _isFinished;
        private SpanContext _context;
        private Stopwatch _sw;

        public ISpanContext Context => _context;

        internal ITraceContext TraceContext => _context.TraceContext;

        internal DateTimeOffset StartTime { get; }

        internal TimeSpan Duration { get; set; }

        internal string OperationName { get; set; }

        internal string ResourceName { get; set; }

        internal string ServiceName => _context.ServiceName;

        internal string Type { get; set; }

        internal bool Error { get; set; }

        internal bool IsRootSpan { get { return _context.ParentId == null; } }

        internal Span(IDatadogTracer tracer, SpanContext parent, string operationName, DateTimeOffset? start)
        {
            _tracer = tracer;
            if(parent != null)
            {
                _context = new SpanContext(parent);
            }
            else
            {
                var traceContext = _tracer.GetTraceContext();
                _context = new SpanContext(traceContext);
            }
            _context.ServiceName = _context.ServiceName ?? _tracer.DefaultServiceName;
            OperationName = operationName;
            if (start.HasValue)
            {
                StartTime = start.Value;
            }
            else
            {
                StartTime = DateTimeOffset.UtcNow;
                _sw = Stopwatch.StartNew();
            }
        }

        public void Dispose()
        {
            Finish();
        }

        public void Finish()
        {
            if (!_isFinished)
            {
                // If the startTime was explicitely provided, we don't use a StopWatch to compute the duration
                if (_sw == null)
                {
                    Finish(DateTimeOffset.UtcNow);
                    return;
                }
                else
                {
                    Duration = _sw.Elapsed;
                }
                _context.TraceContext.CloseSpan(this);
                _isFinished = true;
            }
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            if (!_isFinished)
            {
                Duration = finishTimestamp - StartTime;
                if (Duration < TimeSpan.Zero)
                {
                    Duration = TimeSpan.Zero;
                }
                _context.TraceContext.CloseSpan(this);
                _isFinished = true;
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
            switch (key) {
                case Tags.Service:
                    this._context.ServiceName = value;
                    return this;
                case Tags.Resource:
                    ResourceName = value;
                    return this;
                case Tags.Error:
                    Error = value == "True";
                    return this;
                case Tags.Type:
                    Type = value;
                    return this;
            }
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
