using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    public class Span : IDisposable
    {
        private static ILog _log = LogProvider.For<Span>();

        private object _lock = new object();
        private IDatadogTracer _tracer;
        private Dictionary<string, string> _tags;
        private SpanContext _context;

        internal Span(IDatadogTracer tracer, SpanContext parent, string operationName, string serviceName, DateTimeOffset? start)
        {
            // TODO:bertrand should we throw an exception if operationName is null or empty?
            _tracer = tracer;
            _context = new SpanContext(tracer, parent, serviceName);
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

        public string OperationName { get; set; }

        public string ResourceName { get; set; }

        public string Type { get; set; }

        public bool Error { get; set; }

        internal SpanContext Context => _context;

        internal ITraceContext TraceContext => _context.TraceContext;

        internal DateTimeOffset StartTime { get; }

        internal TimeSpan Duration { get; private set; }

        internal string ServiceName => _context.ServiceName;

        internal bool IsRootSpan => _context.ParentId == null;

        // This is threadsafe only if used after the span has been closed.
        // It is acceptable because this property is internal. But if we were to make it public we would need to add some checks.
        internal IReadOnlyDictionary<string, string> Tags => _tags;

        internal bool IsFinished { get; private set; }

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
            if (Tags != null)
            {
                foreach (var kv in Tags)
                {
                    sb.Append($"\t{kv.Key}:{kv.Value}");
                }
            }

            return sb.ToString();
        }

        public Span SetTag(string key, string value)
        {
            lock (_lock)
            {
                if (IsFinished)
                {
                    _log.Debug("SetTag should not be called after the span was closed");
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

        public void Dispose()
        {
            Finish();
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
    }
}
