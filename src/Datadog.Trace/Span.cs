using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    /// <summary>
    /// A Span represents a logical unit of work in the system. It may be
    /// related to other spans by parent/children relationships. The span
    /// tracks the duration of an operation as well as associated metadata in
    /// the form of a resource name, a service name, and user defined tags.
    /// </summary>
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

        /// <summary>
        /// Gets or sets operation name
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// Gets or sets the resource name
        /// </summary>
        public string ResourceName { get; set; }

        /// <summary>
        /// Gets or sets the type of request this span represents (ex: web, db)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this span represents an error
        /// </summary>
        public bool Error { get; set; }

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => _context.ServiceName;

        /// <summary>
        /// Gets the trace's unique identifier.
        /// </summary>
        public ulong TraceId => _context.TraceId;

        /// <summary>
        /// Gets the span's unique identifier.
        /// </summary>
        public ulong SpanId => _context.SpanId;

        internal SpanContext Context => _context;

        internal ITraceContext TraceContext => _context.TraceContext;

        internal DateTimeOffset StartTime { get; }

        internal TimeSpan Duration { get; private set; }

        // In case we inject a context from another process,
        // the _context.Parent will not be null but TraceContext will be null.
        internal bool IsRootSpan => _context.Parent?.TraceContext == null;

        // This is threadsafe only if used after the span has been closed.
        // It is acceptable because this property is internal. But if we were to make it public we would need to add some checks.
        internal IReadOnlyDictionary<string, string> Tags => _tags;

        internal bool IsFinished { get; private set; }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
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

        /// <summary>
        /// Add a tag metadata to the span
        /// </summary>
        /// <param name="key">The tag's key</param>
        /// <param name="value">The tag's value</param>
        /// <returns> The span object itself</returns>
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

                if (value == null)
                {
                    _tags.Remove(key);
                }
                else
                {
                    _tags[key] = value;
                }

                return this;
            }
        }

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        public void Finish()
        {
            Finish(_context.TraceContext.UtcNow());
        }

        /// <summary>
        /// Explicitly set the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        /// <param name="finishTimestamp">Explicit value for the end time of the Span</param>
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

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        public void Dispose()
        {
            Finish();
        }

        /// <summary>
        /// Add the StackTrace and other exception metadata to the span
        /// </summary>
        /// <param name="exception">The exception.</param>
        public void SetException(Exception exception)
        {
            Error = true;
            SetTag(Trace.Tags.ErrorMsg, exception.Message);
            SetTag(Trace.Tags.ErrorStack, exception.StackTrace);
            SetTag(Trace.Tags.ErrorType, exception.GetType().ToString());
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
