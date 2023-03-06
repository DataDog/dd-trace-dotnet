// <copyright file="Span.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Serilog.Events;
using ActivityStatusCode = System.Diagnostics.ActivityStatusCode;

namespace Datadog.Trace;

/// <summary>
///     Represents a span that is based on an <see cref="Activity"/>.
/// </summary>
internal partial class Span : ISpan
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Span>();
    private static readonly bool IsLogLevelDebugEnabled = Log.IsEnabled(LogEventLevel.Debug);
    private readonly object _lock = new object();
    private System.Diagnostics.Activity _activity;

    internal Span(System.Diagnostics.Activity activity, ITags tags)
    {
        _activity = activity;
        // TODO tags I'm just having for the time being
        Tags = tags ?? new CommonTags();
    }

    internal SpanContext Context { get; set; }

    public string OperationName
    {
        get
        {
            return _activity.OperationName;
        }

        set
        {
            // TODO no setter for OperationName we could custom property this as well or rejit
        }
    }

    public string ResourceName
    {
        get
        {
            return _activity.DisplayName;
        }

        set
        {
            _activity.DisplayName = value;
        }
    }

    // TODO
    public string Type
    {
        get;
        set;
    }

    public bool Error
    {
        get => _activity.Status == ActivityStatusCode.Error;
        set
        {
            if (value)
            {
                _activity.SetStatus(ActivityStatusCode.Error);
            }
            else
            {
                _activity.SetStatus(ActivityStatusCode.Ok); // TODO assuming "false" is Ok and not Unset
            }
        }
    }

    internal bool IsFinished { get; private set; }

    public string ServiceName
    {
        get
        {
            return _activity.GetCustomProperty("ActivitySpanServiceName").ToString();
        }

        set
        {
            _activity.SetCustomProperty("ActivitySpanServiceName", value);
        }
    }

    // HACK TO SUPPORT TAGS - ideally we'd defer everything to Activity Tags
    internal ITags Tags
    {
        get
        {
            return (ITags)_activity.GetCustomProperty(nameof(Tags));
        }

        set
        {
            _activity.SetCustomProperty(nameof(Tags), value);
        }
    }

    public ulong TraceId => Convert.ToUInt64(_activity.TraceId.ToHexString().Substring(16), 16);

    public ulong SpanId => Convert.ToUInt64(_activity.SpanId.ToHexString(), 16);

    internal bool IsRootSpan => Context.TraceContext?.RootSpan == this;

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

    internal DateTimeOffset StartTime
    {
        get
        {
            return _activity.StartTimeUtc;
        }

        // TODO StartTimeUtc is private
        set
        {
            // do nothing for now
        }
    }

    internal TimeSpan Duration { get; private set; }

    internal bool IsTopLevel => Context.Parent == null || Context.Parent.SpanId == 0 || Context.Parent.ServiceName != ServiceName;

    public ISpan SetTag(string key, string value)
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
                default:
                    _activity.SetTag(key, value);
                    break;
            }

            return this;
    }

    internal Span SetMetric(string key, double? value)
    {
        // TODO this should be within the "metrics" - unsure what the best way of doing that is for this
        _activity.SetTag(key, value);
        return this;
    }

    internal double? GetMetric(string key)
    {
        return (double?)_activity.GetTagItem(key);
    }

    public void Finish()
    {
        Finish(Context.TraceContext.ElapsedSince(StartTime));
    }

    public void Finish(DateTimeOffset finishTimestamp)
    {
        Finish(finishTimestamp - StartTime);
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
        switch (key)
        {
            case Trace.Tags.SamplingPriority:
                return Context.TraceContext?.SamplingPriority?.ToString();
            case Trace.Tags.Env:
                return Context.TraceContext?.Environment;
            case Trace.Tags.Version:
                return Context.TraceContext?.ServiceVersion;
            case Trace.Tags.Origin:
                return Context.TraceContext?.Origin;
            default:
                var item = _activity.GetTagItem(key);
                return item?.ToString(); // TODO would this work consistently as Activity tags are string/object pairs
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

            // TODO
            if (IsLogLevelDebugEnabled)
            {
                Log.Debug(
                    "Span closed: [s_id: {SpanId}, p_id: {ParentId}, t_id: {TraceId}] for (Service: {ServiceName}, Resource: {ResourceName}, Operation: {OperationName}, Tags: [{Tags}])",
                    new object[] { SpanId, Context.ParentId, TraceId, ServiceName, ResourceName, OperationName, Tags });
            }
        }
    }

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

    internal void ResetStartTime()
    {
        StartTime = Context.TraceContext.UtcNow;
    }

    internal void SetStartTime(DateTimeOffset startTime)
    {
        StartTime = startTime;
    }

    internal void SetDuration(TimeSpan duration)
    {
        Duration = duration;
    }

    public void Dispose()
    {
        Finish();
    }
}
