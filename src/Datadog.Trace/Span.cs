using System;
using System.Globalization;
using System.Text;
using Datadog.Trace.Abstractions;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace
{
    /// <summary>
    /// A Span represents a logical unit of work in the system. It may be
    /// related to other spans by parent/children relationships. The span
    /// tracks the duration of an operation as well as associated metadata in
    /// the form of a resource name, a service name, and user defined tags.
    /// </summary>
    public class Span : IDisposable, ISpan
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<Span>();
        private static readonly bool IsLogLevelDebugEnabled = Log.IsEnabled(LogEventLevel.Debug);

        private readonly object _lock = new object();

        internal Span(SpanContext context, DateTimeOffset? start)
            : this(context, start, null)
        {
        }

        internal Span(SpanContext context, DateTimeOffset? start, ITags tags)
        {
            Tags = tags ?? new CommonTags();
            Context = context;
            ServiceName = context.ServiceName;
            StartTime = start ?? Context.TraceContext.UtcNow;

            Log.Debug(
                "Span started: [s_id: {SpanID}, p_id: {ParentId}, t_id: {TraceId}]",
                SpanId,
                Context.ParentId,
                TraceId);
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

        internal ITags Tags { get; set; }

        internal SpanContext Context { get; }

        internal DateTimeOffset StartTime { get; private set; }

        internal TimeSpan Duration { get; private set; }

        internal bool IsFinished { get; private set; }

        internal bool IsRootSpan => Context?.TraceContext?.RootSpan == this;

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
            sb.AppendLine($"Meta: {Tags}");

            return sb.ToString();
        }

        /// <summary>
        /// Add a the specified tag to this span.
        /// </summary>
        /// <param name="key">The tag's key.</param>
        /// <param name="value">The tag's value.</param>
        /// <returns>This span to allow method chaining.</returns>
        public Span SetTag(string key, string value)
        {
            if (IsFinished)
            {
                Log.Warning("SetTag should not be called after the span was closed");
                return this;
            }

            // some tags have special meaning
            switch (key)
            {
                case Trace.Tags.SamplingPriority:
                    if (Enum.TryParse(value, out SamplingPriority samplingPriority) &&
                        Enum.IsDefined(typeof(SamplingPriority), samplingPriority))
                    {
                        // allow setting the sampling priority via a tag
                        Context.TraceContext.SamplingPriority = samplingPriority;
                    }

                    break;
#pragma warning disable CS0618 // Type or member is obsolete
                case Trace.Tags.ForceKeep:
                case Trace.Tags.ManualKeep:
                    if (value?.ToBoolean() == true)
                    {
                        // user-friendly tag to set UserKeep priority
                        Context.TraceContext.SamplingPriority = SamplingPriority.UserKeep;
                    }

                    break;
                case Trace.Tags.ForceDrop:
                case Trace.Tags.ManualDrop:
                    if (value?.ToBoolean() == true)
                    {
                        // user-friendly tag to set UserReject priority
                        Context.TraceContext.SamplingPriority = SamplingPriority.UserReject;
                    }

                    break;
#pragma warning restore CS0618 // Type or member is obsolete
                case Trace.Tags.Analytics:
                    if (string.IsNullOrEmpty(value))
                    {
                        // remove metric
                        SetMetric(Trace.Tags.Analytics, null);
                        return this;
                    }

                    // value is a string and can represent a bool ("true") or a double ("0.5"),
                    // so try to parse both. note that "1" and "0" will parse as boolean, which is fine.
                    bool? analyticsSamplingRate = value.ToBoolean();

                    if (analyticsSamplingRate == true)
                    {
                        // always sample
                        SetMetric(Trace.Tags.Analytics, 1.0);
                    }
                    else if (analyticsSamplingRate == false)
                    {
                        // never sample
                        SetMetric(Trace.Tags.Analytics, 0.0);
                    }
                    else if (double.TryParse(
                        value,
                        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                        CultureInfo.InvariantCulture,
                        out double analyticsSampleRate))
                    {
                        // use specified sample rate
                        SetMetric(Trace.Tags.Analytics, analyticsSampleRate);
                    }
                    else
                    {
                        Log.Warning("Value {Value} has incorrect format for tag {TagName}", value, Trace.Tags.Analytics);
                    }

                    break;
                case Trace.Tags.Measured:
                    if (string.IsNullOrEmpty(value))
                    {
                        // Remove metric if value is null
                        SetMetric(Trace.Tags.Measured, null);
                        return this;
                    }

                    bool? measured = value.ToBoolean();

                    if (measured == true)
                    {
                        // Set metric to true by passing the value of 1.0
                        SetMetric(Trace.Tags.Measured, 1.0);
                    }
                    else if (measured == false)
                    {
                        // Set metric to false by passing the value of 0.0
                        SetMetric(Trace.Tags.Measured, 0.0);
                    }
                    else
                    {
                        Log.Warning("Value {Value} has incorrect format for tag {TagName}", value, Trace.Tags.Measured);
                    }

                    break;
                default:
                    Tags.SetTag(key, value);
                    break;
            }

            return this;
        }

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
            Finish(Context.TraceContext.ElapsedSince(StartTime));
        }

        /// <summary>
        /// Explicitly set the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        /// <param name="finishTimestamp">Explicit value for the end time of the Span</param>
        public void Finish(DateTimeOffset finishTimestamp)
        {
            Finish(finishTimestamp - StartTime);
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
                SetTag(Trace.Tags.ErrorStack, exception.ToString());
                SetTag(Trace.Tags.ErrorType, exception.GetType().ToString());
            }
        }

        /// <summary>
        /// Gets the value (or default/null if the key is not a valid tag) of a tag with the key value passed
        /// </summary>
        /// <param name="key">The tag's key</param>
        /// <returns> The value for the tag with the key specified, or null if the tag does not exist</returns>
        public string GetTag(string key)
        {
            switch (key)
            {
                case Trace.Tags.SamplingPriority:
                    return ((int?)(Context.TraceContext?.SamplingPriority ?? Context.SamplingPriority))?.ToString();
                default:
                    return Tags.GetTag(key);
            }
        }

        internal void Finish(TimeSpan duration)
        {
            var shouldCloseSpan = false;
            lock (_lock)
            {
                ResourceName ??= OperationName;

                if (!IsFinished)
                {
                    Duration = duration;
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

                if (IsLogLevelDebugEnabled)
                {
                    Log.Debug(
                        "Span closed: [s_id: {SpanId}, p_id: {ParentId}, t_id: {TraceId}] for (Service: {ServiceName}, Resource: {ResourceName}, Operation: {OperationName}, Tags: [{Tags}])",
                        SpanId,
                        Context.ParentId,
                        TraceId,
                        ServiceName,
                        ResourceName,
                        OperationName,
                        Tags);
                }
            }
        }

        internal double? GetMetric(string key)
        {
            return Tags.GetMetric(key);
        }

        internal Span SetMetric(string key, double? value)
        {
            Tags.SetMetric(key, value);

            return this;
        }

        internal void ResetStartTime()
        {
            StartTime = Context.TraceContext.UtcNow;
        }
    }
}
