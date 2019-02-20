using System;
using System.Collections.Concurrent;
using System.Text;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Interfaces;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    /// <summary>
    ///     A Span represents a logical unit of work in the system. It may be
    ///     related to other spans by parent/children relationships. The span
    ///     tracks the duration of an operation as well as associated metadata in
    ///     the form of a resource name, a service name, and user defined tags.
    /// </summary>
    public class Span : IDisposable, ISpan
    {
        private static readonly ILog _log = LogProvider.For<Span>();
        private readonly object _lock = new object();
        private readonly IDatadogTracer _tracer;

        internal Span(IDatadogTracer tracer, SpanContext parent, string operationName, string serviceName, DateTimeOffset? start)
        {
            // TODO:bertrand should we throw an exception if operationName is null or empty?
            _tracer = tracer;
            Context = new SpanContext(tracer, parent, serviceName);
            OperationName = operationName;

            StartTime = start ?? Context.TraceContext.UtcNow();
        }

        /// <summary>
        ///     Gets or sets operation name
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        ///     Gets or sets the resource name
        /// </summary>
        public string ResourceName { get; set; }

        /// <summary>
        ///     Gets or sets the type of request this span represents (ex: web, db)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether this span represents an error
        /// </summary>
        public bool Error { get; set; }

        /// <summary>
        ///     Gets or sets the service name
        /// </summary>
        public string ServiceName
        {
            get => Context.ServiceName;
            set => Context.ServiceName = value;
        }

        /// <summary>
        ///     Gets the trace's unique identifier.
        /// </summary>
        public ulong TraceId => Context.TraceId;

        /// <summary>
        ///     Gets the span's unique identifier.
        /// </summary>
        public ulong SpanId => Context.SpanId;

        /// <summary>
        ///     Gets a value indicating whether a span is completed or not
        /// </summary>
        public bool IsFinished { get; private set; }

        internal ConcurrentDictionary<string, string> Tags { get; } = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal SpanContext Context { get; }

        internal ITraceContext TraceContext => Context.TraceContext;

        internal DateTimeOffset StartTime { get; }

        internal TimeSpan Duration { get; private set; }

        // In case we inject a context from another process,
        // the _context.Parent will not be null but TraceContext will be null.
        internal bool IsRootSpan => Context.Parent?.TraceContext == null;

        /// <summary>
        ///     Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        ///     A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"TraceId: {Context.TraceId}");
            sb.AppendLine($"ParentId: {Context.ParentId}");
            sb.AppendLine($"SpanId: {Context.SpanId}");
            sb.AppendLine($"ServiceName: {Context.ServiceName}");
            sb.AppendLine($"OperationName: {OperationName}");
            sb.AppendLine($"Resource: {ResourceName}");
            sb.AppendLine($"Type: {Type}");
            sb.AppendLine($"Start: {StartTime}");
            sb.AppendLine($"Duration: {Duration}");
            sb.AppendLine($"Error: {Error}");
            sb.AppendLine("Meta:");

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
        ///     Record the end time of the span and flushes it to the backend.
        ///     After the span has been finished all modifications will be ignored.
        /// </summary>
        public void Finish()
        {
            Finish(Context.TraceContext.UtcNow());
        }

        /// <summary>
        ///     Explicitly set the end time of the span and flushes it to the backend.
        ///     After the span has been finished all modifications will be ignored.
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
                Context.TraceContext.CloseSpan(this);
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///     Record the end time of the span and flushes it to the backend.
        ///     After the span has been finished all modifications will be ignored.
        /// </summary>
        public void Dispose()
        {
            Finish();
        }

        /// <summary>
        ///     Add the StackTrace and other exception metadata to the span
        /// </summary>
        /// <param name="exception">The exception.</param>
        public void SetException(Exception exception)
            => SpanExtensions.SetException(this, exception);

        /// <summary>
        ///     Add a tag metadata to the span
        /// </summary>
        /// <param name="key">The tag's key</param>
        /// <param name="value">The tag's value</param>
        /// <returns> The span object itself</returns>
        public Span SetTag(string key, string value)
        {
            Tag(key, value);

            return this;
        }

        /// <summary>
        ///     Proxy to SetTag without return value
        ///     See <see cref="Span.SetTag(string, string)" /> for more information
        /// </summary>
        /// <param name="key">The tag's key</param>
        /// <param name="value">The tag's value</param>
        public void Tag(string key, string value)
        {
            if (IsFinished)
            {
                _log.Debug("SetTag should not be called after the span was closed");

                return;
            }

            if (value == null)
            {
                Tags.TryRemove(key, out value);
            }
            else
            {
                Tags[key] = value;
            }
        }

        /// <summary>
        ///     Gets the value (or default/null if the key is not a valid tag) of a tag with the key value passed
        /// </summary>
        /// <param name="key">The tag's key</param>
        /// <returns> The value for the tag with the key specified, or null if the tag does not exist</returns>
        public string GetTag(string key)
            => Tags.TryGetValue(key, out var value)
                   ? value
                   : null;
    }
}
