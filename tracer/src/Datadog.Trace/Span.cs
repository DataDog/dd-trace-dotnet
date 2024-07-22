// <copyright file="Span.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace
{
    /// <summary>
    /// A Span represents a logical unit of work in the system. It may be
    /// related to other spans by parent/children relationships. The span
    /// tracks the duration of an operation as well as associated metadata in
    /// the form of a resource name, a service name, and user defined tags.
    /// </summary>
    internal partial class Span : ISpan
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Span>();
        private static readonly bool IsLogLevelDebugEnabled = Log.IsEnabled(LogEventLevel.Debug);

        private int _isFinished;
        private bool _baseServiceTagSet;

        internal Span(SpanContext context, DateTimeOffset? start)
            : this(context, start, null)
        {
        }

        internal Span(SpanContext context, DateTimeOffset? start, ITags tags)
        {
            Tags = tags ?? new CommonTags();
            Context = context;
            StartTime = start ?? Context.TraceContext.Clock.UtcNow;

            if (IsLogLevelDebugEnabled)
            {
                WriteCtorDebugMessage();
            }
        }

        /// <summary>
        /// Gets or sets operation name
        /// </summary>
        internal string OperationName { get; set; }

        /// <summary>
        /// Gets or sets the resource name
        /// </summary>
        internal string ResourceName { get; set; }

        /// <summary>
        /// Gets or sets the type of request this span represents (ex: web, db).
        /// Not to be confused with span kind.
        /// </summary>
        /// <seealso cref="SpanTypes"/>
        internal string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this span represents an error
        /// </summary>
        internal bool Error { get; set; }

        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        internal string ServiceName
        {
            get => Context.ServiceNameInternal;
            set
            {
                // Ignore case because service name and _dd.base_service are normalized in the agent and backend
                if (!_baseServiceTagSet && !string.Equals(value, Context.ServiceNameInternal, StringComparison.OrdinalIgnoreCase))
                {
                    Tags.SetTag(Trace.Tags.BaseService, Context.ServiceNameInternal);
                    _baseServiceTagSet = true;
                }

                Context.ServiceNameInternal = value;
            }
        }

        /// <summary>
        /// Gets the trace's unique 128-bit identifier.
        /// </summary>
        internal TraceId TraceId128 => Context.TraceId128;

        /// <summary>
        /// Gets the 64-bit trace id, or the lower 64 bits of a 128-bit trace id.
        /// </summary>
        internal ulong TraceId => Context.TraceId128.Lower;

        /// <summary>
        /// Gets the span's unique 64-bit identifier.
        /// </summary>
        internal ulong SpanId => Context.SpanId;

        /// <summary>
        /// Gets <i>local root span id</i>, i.e. the <c>SpanId</c> of the span that is the root of the local, non-reentrant
        /// sub-operation of the distributed operation that is represented by the trace that contains this span.
        /// </summary>
        /// <remarks>
        /// <para>If the trace has been propagated from a remote service, the <i>remote global root</i> is not relevant for this API.</para>
        /// <para>A distributed operation represented by a trace may be re-entrant (e.g. service-A calls service-B, which calls service-A again).
        /// In such cases, the local process may be concurrently executing multiple local root spans.
        /// This API returns the id of the root span of the non-reentrant trace sub-set.</para></remarks>
        internal ulong RootSpanId => Context.TraceContext?.RootSpan?.SpanId ?? SpanId;

        internal ITags Tags { get; set; }

        internal SpanContext Context { get; }

        internal List<SpanLink> SpanLinks { get; private set; }

        internal DateTimeOffset StartTime { get; private set; }

        internal TimeSpan Duration { get; private set; }

        internal bool IsFinished
        {
            get => _isFinished == 1;
            private set => _isFinished = value ? 1 : 0;
        }

        internal bool IsRootSpan => Context.TraceContext?.RootSpan == this;

        internal bool IsTopLevel => Context.ParentInternal == null
                                 || Context.ParentInternal.SpanId == 0
                                 || Context.ParentInternal switch
                                 {
                                     SpanContext s => s.ServiceNameInternal != ServiceName,
                                     { } s => s.ServiceName != ServiceName,
                                 };

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        public void Dispose()
        {
            Finish();
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            sb.AppendLine($"TraceId64: {Context.TraceId128.Lower}");
            sb.AppendLine($"TraceId128: {Context.TraceId128}");
            sb.AppendLine($"RawTraceId: {Context.RawTraceId}");
            sb.AppendLine($"ParentId: {Context.ParentIdInternal}");
            sb.AppendLine($"SpanId: {Context.SpanId}");
            sb.AppendLine($"RawSpanId: {Context.RawSpanId}");
            sb.AppendLine($"Origin: {Context.Origin}");
            sb.AppendLine($"ServiceName: {ServiceName}");
            sb.AppendLine($"OperationName: {OperationName}");
            sb.AppendLine($"Resource: {ResourceName}");
            sb.AppendLine($"Type: {Type}");
            sb.AppendLine($"Start: {StartTime:O}");
            sb.AppendLine($"Duration: {Duration}");
            sb.AppendLine($"End: {StartTime.Add(Duration):O}");
            sb.AppendLine($"Error: {Error}");

            var samplingPriority = Context.TraceContext?.SamplingPriority;
            sb.AppendLine($"TraceSamplingPriority: {SamplingPriorityValues.ToString(samplingPriority) ?? "not set"}");

            sb.AppendLine($"Meta: {Tags}");

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Add a the specified tag to this span.
        /// </summary>
        /// <param name="key">The tag's key.</param>
        /// <param name="value">The tag's value.</param>
        /// <returns>This span to allow method chaining.</returns>
        internal ISpan SetTag(string key, string value)
        {
            if (IsFinished)
            {
                Log.Warning("SetTag should not be called after the span was closed");
                return this;
            }

            static void LogMissingTraceContext(string key, string value)
            {
                Log.Warning("Ignoring ISpan.SetTag({Key}, {Value}) because the span is not associated to a TraceContext.", key, value);
            }

            // since we don't expose a public API for setting trace-level attributes yet,
            // allow setting them through any span in the trace.
            // also, some "pseudo-tags" have special meaning, such as "manual.keep" and "_dd.measured".
            switch (key)
            {
                case Trace.Tags.Env:
                    if (Context.TraceContext == null)
                    {
                        LogMissingTraceContext(key, value);
                        return this;
                    }

                    Context.TraceContext.Environment = value;
                    break;
                case Trace.Tags.Version:
                    if (Context.TraceContext == null)
                    {
                        LogMissingTraceContext(key, value);
                        return this;
                    }

                    Context.TraceContext.ServiceVersion = value;
                    break;
                case Trace.Tags.Origin:
                    if (Context.TraceContext == null)
                    {
                        LogMissingTraceContext(key, value);
                        return this;
                    }

                    Context.TraceContext.Origin = value;
                    break;

                case Trace.Tags.AzureAppServicesSiteName:
                case Trace.Tags.AzureAppServicesSiteKind:
                case Trace.Tags.AzureAppServicesSiteType:
                case Trace.Tags.AzureAppServicesResourceGroup:
                case Trace.Tags.AzureAppServicesSubscriptionId:
                case Trace.Tags.AzureAppServicesResourceId:
                case Trace.Tags.AzureAppServicesInstanceId:
                case Trace.Tags.AzureAppServicesInstanceName:
                case Trace.Tags.AzureAppServicesOperatingSystem:
                case Trace.Tags.AzureAppServicesRuntime:
                case Trace.Tags.AzureAppServicesExtensionVersion:
                    Log.Warning("This tag is reserved for Azure App Service tagging. Value will be ignored");
                    break;

                case Trace.Tags.SamplingPriority:
                    if (Context.TraceContext == null)
                    {
                        LogMissingTraceContext(key, value);
                        return this;
                    }

                    // note: this tag allows numeric or string representations of the enum,
                    // (e.g. "AutoKeep" or "1"), but try parsing as `int` first since it's much faster
                    if (int.TryParse(value, out var samplingPriorityInt32))
                    {
                        Context.TraceContext.SetSamplingPriority(samplingPriorityInt32, SamplingMechanism.Manual);
                    }
                    else if (Enum.TryParse<SamplingPriority>(value, out var samplingPriorityEnum))
                    {
                        Context.TraceContext.SetSamplingPriority((int?)samplingPriorityEnum, SamplingMechanism.Manual);
                    }

                    break;
                case Trace.Tags.ManualKeep:
                    if (Context.TraceContext == null)
                    {
                        LogMissingTraceContext(key, value);
                        return this;
                    }

                    if (value?.ToBoolean() == true)
                    {
                        // user-friendly tag to set UserKeep priority
                        Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Manual);
                    }

                    break;
                case Trace.Tags.ManualDrop:
                    if (Context.TraceContext == null)
                    {
                        LogMissingTraceContext(key, value);
                        return this;
                    }

                    if (value?.ToBoolean() == true)
                    {
                        // user-friendly tag to set UserReject priority
                        Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
                    }

                    break;
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
                case Trace.Tags.PeerService:
                    Tags.SetTag(key, value);
                    Context.TraceContext.CurrentTraceSettings.Schema.RemapPeerService(Tags);
                    break;
                default:
                    Tags.SetTag(key, value);
                    break;
            }

            return this;
        }

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        internal void Finish()
        {
            Finish(Context.TraceContext.Clock.ElapsedSince(StartTime));
        }

        /// <summary>
        /// Explicitly set the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        /// <param name="finishTimestamp">Explicit value for the end time of the Span</param>
        internal void Finish(DateTimeOffset finishTimestamp)
        {
            Finish(finishTimestamp - StartTime);
        }

        /// <summary>
        /// Add the StackTrace and other exception metadata to the span
        /// </summary>
        /// <param name="exception">The exception.</param>
        internal void SetException(Exception exception)
        {
            // We do not log BlockExceptions as errors
            if (exception is not AppSec.BlockException)
            {
                Error = true;
                SetExceptionTags(exception);
            }
        }

        /// <summary>
        /// Add the StackTrace and other exception metadata to the span,
        /// but does not mark the span as an error.
        /// </summary>
        /// <param name="exception">The exception.</param>
        internal void SetExceptionTags(Exception exception)
        {
            if (exception != null && exception is not AppSec.BlockException)
            {
                try
                {
                    // for AggregateException, use the first inner exception until we can support multiple errors.
                    // there will be only one error in most cases, and even if there are more and we lose
                    // the other ones, it's still better than the generic "one or more errors occurred" message.
                    if (exception is AggregateException { InnerExceptions.Count: > 0 } aggregateException)
                    {
                        exception = aggregateException.InnerExceptions[0];
                    }

                    SetTag(Trace.Tags.ErrorMsg, exception.Message);
                    SetTag(Trace.Tags.ErrorType, exception.GetType().ToString());
                    SetTag(Trace.Tags.ErrorStack, exception.ToString());

                    ExceptionDebugging.Report(this, exception);
                }
                catch (Exception ex)
                {
                    // We have found rare cases where exception.ToString() throws an exception, such as in a FileNotFoundException
                    Log.Warning(ex, "Error setting exception tags on span {SpanId} in trace {TraceId128}", SpanId, TraceId128);
                }
            }
        }

        /// <summary>
        /// Gets the value of the specified tag.
        /// </summary>
        /// <param name="key">The tag's key</param>
        /// <returns>The value for the tag with the specified key, or <c>null</c> if the tag does not exist.</returns>
        internal string GetTag(string key)
        {
            // since we don't expose a public API for getting trace-level attributes yet,
            // allow retrieval through any span in the trace
            return key switch
            {
                Trace.Tags.SamplingPriority => SamplingPriorityValues.ToString(Context.TraceContext?.SamplingPriority),
                Trace.Tags.Env => Context.TraceContext?.Environment,
                Trace.Tags.Version => Context.TraceContext?.ServiceVersion,
                Trace.Tags.Origin => Context.TraceContext?.Origin,
                Trace.Tags.TraceId => Context.RawTraceId,
                _ => Tags.GetTag(key)
            };
        }

        internal void Finish(TimeSpan duration)
        {
            ResourceName ??= OperationName;
            if (Interlocked.CompareExchange(ref _isFinished, 1, 0) == 0)
            {
                if (IsRootSpan)
                {
                    ExceptionDebugging.EndRequest();
                }

                Duration = duration;
                if (Duration < TimeSpan.Zero)
                {
                    Duration = TimeSpan.Zero;
                }

                Context.TraceContext.CloseSpan(this);

                if (IsLogLevelDebugEnabled)
                {
                    WriteCloseDebugMessage();
                }

                TelemetryFactory.Metrics.RecordCountSpanFinished();
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

        internal Span SetMetaStruct(string key, byte[] value)
        {
            Tags.SetMetaStruct(key, value);

            return this;
        }

        internal void ResetStartTime()
        {
            StartTime = Context.TraceContext.Clock.UtcNow;
        }

        internal void SetStartTime(DateTimeOffset startTime)
        {
            StartTime = startTime;
        }

        internal void SetDuration(TimeSpan duration)
        {
            Duration = duration;
        }

        internal void MarkSpanForExceptionDebugging()
        {
            ExceptionDebugging.BeginRequest();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteCtorDebugMessage()
        {
            var tagsType = Tags.GetType();

            Log.Debug(
                "Span started: [s_id: {SpanId}, p_id: {ParentId}, t_id: {TraceId}] with Tags: [{Tags}], Tags Type: [{TagsType}])",
                new object[] { Context.RawSpanId, Context.ParentIdInternal, Context.RawTraceId, Tags, tagsType });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteCloseDebugMessage()
        {
            Log.Debug(
                "Span closed: [s_id: {SpanId}, p_id: {ParentId}, t_id: {TraceId}] for (Service: {ServiceName}, Resource: {ResourceName}, Operation: {OperationName}, Tags: [{Tags}])\nDetails:{ToString}",
                new object[] { Context.RawSpanId, Context.ParentIdInternal, Context.RawTraceId, ServiceName, ResourceName, OperationName, Tags, ToString() });
        }

        /// <summary>
        /// Adds a SpanLink to the current Span if the Span is active.
        /// </summary>
        /// <param name="spanLinkToAdd">The Span to add as a SpanLink</param>
        /// <param name="attributes">List of KeyValue pairings of attributes to add to the SpanLink. Defaults to null</param>
        /// <returns>returns the SpanLink on success or null on failure (span is closed already)</returns>
        internal SpanLink AddSpanLink(Span spanLinkToAdd, List<KeyValuePair<string, string>> attributes = null)
        {
            if (IsFinished)
            {
                Log.Warning("AddSpanLink should not be called after the span was closed");
                return null;
            }

            SpanLinks ??= new List<SpanLink>();
            var spanLink = new SpanLink(spanLinkToAdd, this, attributes);
            SpanLinks.Add(spanLink);
            return spanLink;
        }
    }
}
