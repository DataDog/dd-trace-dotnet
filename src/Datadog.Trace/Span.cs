using System;
using System.Collections.Concurrent;
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
        private static readonly ILog Log = LogProvider.For<Span>();

        private readonly object _lock = new object();

        internal Span(IDatadogTracer tracer, ISpanContext parent, DateTimeOffset? start)
        {
            // TODO:bertrand should we throw an exception if operationName is null or empty?
            Context = new SpanContext(tracer, parent);
            StartTime = start ?? Context.TraceContext.UtcNow;
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
        /// Gets or sets the type of request this span represents (ex: web, db).
        /// Not to be confused with span kind.
        /// </summary>
        /// <seealso cref="SpanTypes"/>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this span represents an error
        /// </summary>
        public bool Error { get; set; }

        /// <summary>
        /// Gets or sets the service name
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets the trace's unique identifier.
        /// </summary>
        [Obsolete("This property will be removed in future versions of this library. Use Span.Context.TraceId instead.")]
        public ulong TraceId => Context.TraceId;

        /// <summary>
        /// Gets the span's unique identifier.
        /// </summary>
        [Obsolete("This property will be removed in future versions of this library. Use Span.Context.SpanId instead.")]
        public ulong SpanId => Context.SpanId;

        internal SpanContext Context { get; }

        internal DateTimeOffset StartTime { get; }

        internal TimeSpan Duration { get; private set; }

        internal ConcurrentDictionary<string, string> Tags { get; } = new ConcurrentDictionary<string, string>();

        internal ConcurrentDictionary<string, int> Metrics { get; } = new ConcurrentDictionary<string, int>();

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
            sb.AppendLine($"TraceId: {Context.TraceId}");
            sb.AppendLine($"ParentId: {Context.ParentId}");
            sb.AppendLine($"SpanId: {Context.SpanId}");
            sb.AppendLine($"ServiceName: {ServiceName}");
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

            sb.AppendLine("Metrics:");

            if (Metrics != null && Metrics.Count > 0)
            {
                foreach (var kv in Metrics)
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
            if (IsFinished)
            {
                Log.Debug("SetTag should not be called after the span was closed");
                return this;
            }

            if (value == null)
            {
                Tags.TryRemove(key, out _);
            }
            else
            {
                Tags[key] = value;
            }

            return this;
        }

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        public void Finish()
        {
            Finish(Context.TraceContext.UtcNow);
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
                Context.TraceContext.CloseSpan(this);
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

            if (exception != null)
            {
                // for AggregateException, use the first inner exception until we can support multiple errors.
                // there will be only one error in most cases, and even if there are more and we lose
                // the other ones, it's still better than the generic "one or more errors occurred" message.
                if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count > 0)
                {
                    exception = aggregateException.InnerExceptions[0];
                }

                SetTag(Trace.Tags.ErrorMsg, exception.Message);
                SetTag(Trace.Tags.ErrorStack, exception.StackTrace);
                SetTag(Trace.Tags.ErrorType, exception.GetType().ToString());
            }
        }

        internal bool SetExceptionForFilter(Exception exception)
        {
            SetException(exception);
            return false;
        }

        internal string GetTag(string key)
        {
            return Tags.TryGetValue(key, out string value)
                       ? value
                       : null;
        }

        internal int? GetMetric(string key)
        {
            return Metrics.TryGetValue(key, out int value)
                       ? value
                       : default;
        }

        internal Span SetMetric(string key, int? value)
        {
            if (IsFinished)
            {
                Log.Debug("SetMetric should not be called after the span was closed");
                return this;
            }

            if (value == null)
            {
                Metrics.TryRemove(key, out _);
            }
            else
            {
                Metrics[key] = value.Value;
            }

            return this;
        }
    }
}
