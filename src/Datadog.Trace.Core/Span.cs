using System;
using System.Collections.Generic;
using Datadog.Trace.Abstractions;

namespace Datadog.Trace
{
    /// <summary>
    /// A Span represents a logical unit of work in the system. It may be
    /// related to other spans by parent/children relationships. The span
    /// tracks the duration of an operation as well as associated metadata in
    /// the form of a resource name, a service name, and user defined tags.
    /// </summary>
    public abstract class Span : IDisposable, ISpan
    {
        internal Span(SpanContext context, DateTimeOffset? start)
        {
            Context = context;
            ServiceName = context.ServiceName;
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
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this span represents an error
        /// </summary>
        public bool Error { get; set; }

        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        public string ServiceName
        {
            get => Context.ServiceName;
            set => Context.ServiceName = value;
        }

        /// <summary>
        /// Gets the trace's unique identifier.
        /// </summary>
        public ulong TraceId => Context.TraceId;

        /// <summary>
        /// Gets the span's unique identifier.
        /// </summary>
        public ulong SpanId => Context.SpanId;

        internal SpanContext Context { get; }

        internal DateTimeOffset StartTime { get; }

        internal TimeSpan Duration { get; set; }

        internal Dictionary<string, string> Tags { get; set; }

        internal Dictionary<string, double> Metrics { get; set; }

        internal bool IsFinished { get; set; }

        internal bool IsRootSpan => Context?.TraceContext?.RootSpan == this;

        /// <summary>
        /// Add a the specified tag to this span.
        /// </summary>
        /// <param name="key">The tag's key.</param>
        /// <param name="value">The tag's value.</param>
        /// <returns>This span to allow method chaining.</returns>
        public abstract Span SetTag(string key, string value);

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        /// <param name="finishTimestamp">Explicit value for the end time of the Span</param>
        public abstract void Finish(DateTimeOffset finishTimestamp);

        /// <summary>
        /// Add the StackTrace and other exception metadata to the span
        /// </summary>
        /// <param name="exception">The exception.</param>
        public abstract void SetException(Exception exception);

        /// <summary>
        /// Gets the value (or default/null if the key is not a valid tag) of a tag with the key value passed
        /// </summary>
        /// <param name="key">The tag's key</param>
        /// <returns> The value for the tag with the key specified, or null if the tag does not exist</returns>
        public abstract string GetTag(string key);

        /// <summary>
        /// Run logic needed to close the span.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Add a the specified tag to this span.
        /// </summary>
        /// <param name="key">The tag's key.</param>
        /// <param name="value">The tag's value.</param>
        /// <returns>This span to allow method chaining.</returns>
        ISpan ISpan.SetTag(string key, string value)
            => SetTag(key, value);

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        public void Finish()
        {
            Finish(Context.TraceContext.UtcNow);
        }

        internal abstract Span SetMetric(string key, double? value);
    }
}
