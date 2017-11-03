using Datadog.Trace.Logging;
using OpenTracing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace
{
    public class Span : ISpan
    {
        private static ILog _log = LogProvider.For<Span>();

        private Object _lock = new Object();
        private IDatadogTracer _tracer;
        private Dictionary<string, string> _tags;
        private SpanContext _context;

        ISpanContext ISpan.Context => _context;

        internal SpanContext Context => _context;

        internal ITraceContext TraceContext => _context.TraceContext;

        internal DateTimeOffset StartTime { get; }

        internal TimeSpan Duration { get; private set; }

        internal string OperationName { get; private set; }

        internal string ResourceName { get; private set; }

        internal string ServiceName => _context.ServiceName;

        internal string Type { get; private set; }

        internal bool Error { get; private set; }

        internal bool IsRootSpan { get { return _context.ParentId == null; } }

        // This is threadsafe only if used after the span has been closed.
        // It is acceptable because this property is internal. But if we were to make it public we would need to add some checks.
        internal IReadOnlyDictionary<string, string> Tags { get { return _tags; } }

        internal bool IsFinished { get; private set; }

        internal Span(IDatadogTracer tracer, SpanContext parent, string operationName, string serviceName, DateTimeOffset? start)
        {
            _tracer = tracer;
            _context = new SpanContext(parent?.TraceContext ?? _tracer.GetTraceContext(), serviceName);
            OperationName = operationName;
            if (start.HasValue)
            {
                StartTime = start.Value;
            }
            else
            {
                StartTime = _context.TraceContext.UtcNow();
            }
        }

        public void Dispose()
        {
            Finish();
        }

        public void Finish()
        {
            Finish(_context.TraceContext.UtcNow());
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            var shouldCloseSpan = false;
            lock (_lock)
            {
                ResourceName = ResourceName ?? OperationName;
                if (!IsFinished)
                {
                    Duration = finishTimestamp - StartTime;
                    if (Duration < TimeSpan.Zero)
                    {
                        Duration = TimeSpan.Zero;
                    }
                    IsFinished = true;
                    shouldCloseSpan = true;
                }
            }
            if (shouldCloseSpan)
            {
                _context.TraceContext.CloseSpan(this);
            }
        }

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
            lock (_lock)
            {
                OperationName = operationName;
                return this;
            }
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
            lock (_lock)
            {
                if (IsFinished)
                {
                    _log.Debug("SetTag should not be called after the span was closed");
                    return this;
                }
                switch (key) {
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
                if (_tags == null)
                {
                    _tags = new Dictionary<string, string>();
                }
                _tags[key] = value;
                return this;
            }
        }

        internal string GetTag(string key)
        {
            lock (_lock)
            {
                string s = null;
                _tags?.TryGetValue(key, out s);
                return s;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"TraceId: {_context.TraceId}");
            sb.AppendLine($"ParentId: {_context.ParentId}");
            sb.AppendLine($"SpanId: {_context.SpanId}");
            sb.AppendLine($"ServiceName: {_context.ServiceName}");
            sb.AppendLine($"OperationName: {OperationName}");
            sb.AppendLine($"Resource: {ResourceName}");
            sb.AppendLine($"Type: {Type}");
            sb.AppendLine($"Start: {StartTime}");
            sb.AppendLine($"Duration: {Duration}");
            sb.AppendLine($"Error: {Error}");
            sb.AppendLine($"Meta:");
            if(Tags != null)
            {
                foreach(var kv in Tags)
                {
                    sb.Append($"\t{kv.Key}:{kv.Value}");
                }
            }
            return sb.ToString();
        }
    }
}
