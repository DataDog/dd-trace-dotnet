using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;
using ThreadSafeRandom = Datadog.Trace.Util.ThreadSafeRandom;

namespace Datadog.Trace;
#pragma warning disable SA1201
#pragma warning disable SA1402
#pragma warning disable SA1201

internal partial class SpanData
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanData>();
    private static readonly bool IsLogLevelDebugEnabled = Log.IsEnabled(LogEventLevel.Debug);

    private TraceId _traceId128;
    private ulong _spanId;
    private string _rawTraceId;
    private string _rawSpanId;
    private string _origin;

    private string _resourceName;
    private string _operationName;
    private string _serviceName;
    private string _type;
    private DateTimeOffset _startTime;
    private TimeSpan _duration;
    private int _isFinished;
    private bool _error;
    private ITags _tags;
    private TraceTagCollection _propagatedTags;
    private int? _samplingPriority;
    private string _additionalW3CTraceState;
    private PathwayContext? _pathwayContext;

    private ISpanContext _parent;
    private TraceContext _traceContext;
}

internal partial class SpanData : ISpanInternal
{
    string ISpan.OperationName
    {
        get => _operationName;
        set => _operationName = value;
    }

    string ISpan.ResourceName
    {
        get => _resourceName;
        set => _resourceName = value;
    }

    string ISpan.Type
    {
        get => _type;
        set => _type = value;
    }

    bool ISpan.Error
    {
        get => _error;
        set => _error = value;
    }

    string ISpan.ServiceName
    {
        get => _serviceName;
        set => _serviceName = value;
    }

    TraceId ISpanInternal.TraceId128 => _traceId128;

    ulong ISpan.TraceId => _traceId128.Lower;

    ulong ISpan.SpanId => _spanId;

    ulong ISpanInternal.RootSpanId => _traceContext?.RootSpan?.SpanId ?? _spanId;

    ITags ISpanInternal.Tags
    {
        get => _tags;
        set => _tags = value;
    }

    ISpanContext ISpan.Context => this;

    ISpanContextInternal ISpanInternal.Context => this;

    DateTimeOffset ISpanInternal.StartTime => _startTime;

    TimeSpan ISpanInternal.Duration => _duration;

    bool ISpanInternal.IsFinished => _isFinished == 1;

    bool ISpanInternal.IsRootSpan => (object)_traceContext?.RootSpan == (object)this;

    bool ISpanInternal.IsTopLevel => _parent == null || _parent.SpanId == 0 || _parent.ServiceName != _serviceName;

    ISpan ISpan.SetTag(string key, string value)
    {
        if (((ISpanInternal)this).IsFinished)
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
                if (_traceContext == null)
                {
                    LogMissingTraceContext(key, value);
                    return this;
                }

                _traceContext.Environment = value;
                break;
            case Trace.Tags.Version:
                if (_traceContext == null)
                {
                    LogMissingTraceContext(key, value);
                    return this;
                }

                _traceContext.ServiceVersion = value;
                break;
            case Trace.Tags.Origin:
                if (_traceContext == null)
                {
                    LogMissingTraceContext(key, value);
                    return this;
                }

                _traceContext.Origin = value;
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
                if (_traceContext == null)
                {
                    LogMissingTraceContext(key, value);
                    return this;
                }

                // note: this tag allows numeric or string representations of the enum,
                // (e.g. "AutoKeep" or "1"), but try parsing as `int` first since it's much faster
                if (int.TryParse(value, out var samplingPriorityInt32))
                {
                    _traceContext.SetSamplingPriority(samplingPriorityInt32, SamplingMechanism.Manual);
                }
                else if (Enum.TryParse<SamplingPriority>(value, out var samplingPriorityEnum))
                {
                    _traceContext.SetSamplingPriority((int?)samplingPriorityEnum, SamplingMechanism.Manual);
                }

                break;
            case Trace.Tags.ManualKeep:
                if (_traceContext == null)
                {
                    LogMissingTraceContext(key, value);
                    return this;
                }

                if (value?.ToBoolean() == true)
                {
                    // user-friendly tag to set UserKeep priority
                    _traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Manual);
                }

                break;
            case Trace.Tags.ManualDrop:
                if (_traceContext == null)
                {
                    LogMissingTraceContext(key, value);
                    return this;
                }

                if (value?.ToBoolean() == true)
                {
                    // user-friendly tag to set UserReject priority
                    _traceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
                }

                break;
            case Trace.Tags.Analytics:
                if (string.IsNullOrEmpty(value))
                {
                    // remove metric
                    ((ISpanInternal)this).SetMetric(Trace.Tags.Analytics, null);
                    return this;
                }

                // value is a string and can represent a bool ("true") or a double ("0.5"),
                // so try to parse both. note that "1" and "0" will parse as boolean, which is fine.
                bool? analyticsSamplingRate = value.ToBoolean();

                if (analyticsSamplingRate == true)
                {
                    // always sample
                    ((ISpanInternal)this).SetMetric(Trace.Tags.Analytics, 1.0);
                }
                else if (analyticsSamplingRate == false)
                {
                    // never sample
                    ((ISpanInternal)this).SetMetric(Trace.Tags.Analytics, 0.0);
                }
                else if (double.TryParse(
                    value,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                    CultureInfo.InvariantCulture,
                    out double analyticsSampleRate))
                {
                    // use specified sample rate
                    ((ISpanInternal)this).SetMetric(Trace.Tags.Analytics, analyticsSampleRate);
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
                    ((ISpanInternal)this).SetMetric(Trace.Tags.Measured, null);
                    return this;
                }

                bool? measured = value.ToBoolean();

                if (measured == true)
                {
                    // Set metric to true by passing the value of 1.0
                    ((ISpanInternal)this).SetMetric(Trace.Tags.Measured, 1.0);
                }
                else if (measured == false)
                {
                    // Set metric to false by passing the value of 0.0
                    ((ISpanInternal)this).SetMetric(Trace.Tags.Measured, 0.0);
                }
                else
                {
                    Log.Warning("Value {Value} has incorrect format for tag {TagName}", value, Trace.Tags.Measured);
                }

                break;
            default:
                _tags.SetTag(key, value);
                break;
        }

        return this;
    }

    void ISpan.Finish()
    {
        ((ISpanInternal)this).Finish(_traceContext.ElapsedSince(_startTime));
    }

    void ISpan.Finish(DateTimeOffset finishTimestamp)
    {
        ((ISpanInternal)this).Finish(finishTimestamp - _startTime);
    }

    void ISpan.SetException(Exception exception)
    {
        _error = true;

        if (exception != null)
        {
            // for AggregateException, use the first inner exception until we can support multiple errors.
            // there will be only one error in most cases, and even if there are more and we lose
            // the other ones, it's still better than the generic "one or more errors occurred" message.
            if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count > 0)
            {
                exception = aggregateException.InnerExceptions[0];
            }

            ((ISpanInternal)this).SetTag(Trace.Tags.ErrorMsg, exception.Message);
            ((ISpanInternal)this).SetTag(Trace.Tags.ErrorStack, exception.ToString());
            ((ISpanInternal)this).SetTag(Trace.Tags.ErrorType, exception.GetType().ToString());
        }
    }

    string ISpan.GetTag(string key)
    {
        // since we don't expose a public API for getting trace-level attributes yet,
        // allow retrieval through any span in the trace
        switch (key)
        {
            case Trace.Tags.SamplingPriority:
                return _traceContext?.SamplingPriority?.ToString();
            case Trace.Tags.Env:
                return _traceContext?.Environment;
            case Trace.Tags.Version:
                return _traceContext?.ServiceVersion;
            case Trace.Tags.Origin:
                return _traceContext?.Origin;
            case Trace.Tags.TraceId:
                return _rawTraceId;
            default:
                return _tags.GetTag(key);
        }
    }

    void ISpanInternal.Finish(TimeSpan duration)
    {
        _resourceName ??= _operationName;
        if (Interlocked.CompareExchange(ref _isFinished, 1, 0) == 0)
        {
            _duration = duration;
            if (_duration < TimeSpan.Zero)
            {
                _duration = TimeSpan.Zero;
            }

            _traceContext.CloseSpan(this);

            if (IsLogLevelDebugEnabled)
            {
                Log.Debug(
                    "Span closed: [s_id: {SpanId}, p_id: {ParentId}, t_id: {TraceId}] for (Service: {ServiceName}, Resource: {ResourceName}, Operation: {OperationName}, Tags: [{Tags}])",
                    new object[] { _rawSpanId, ((ISpanContextInternal)this).ParentId, _rawTraceId, _serviceName, _resourceName, _operationName, _tags });
            }
        }
    }

    double? ISpanInternal.GetMetric(string key)
    {
        return _tags.GetMetric(key);
    }

    ISpanInternal ISpanInternal.SetMetric(string key, double? value)
    {
        _tags.SetMetric(key, value);
        return this;
    }

    void ISpanInternal.ResetStartTime()
    {
        _startTime = _traceContext.UtcNow;
    }

    void ISpanInternal.SetStartTime(DateTimeOffset startTime)
    {
        _startTime = startTime;
    }

    void ISpanInternal.SetDuration(TimeSpan duration)
    {
        _duration = duration;
    }

    void IDisposable.Dispose()
    {
        ((ISpan)this).Finish();
    }

    string ISpanInternal.ToString()
    {
        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
        sb.AppendLine($"TraceId64: {_traceId128.Lower}");
        sb.AppendLine($"TraceId128: {_traceId128}");
        sb.AppendLine($"RawTraceId: {_rawTraceId}");
        sb.AppendLine($"ParentId: {((ISpanContextInternal)this).ParentId}");
        sb.AppendLine($"SpanId: {_spanId}");
        sb.AppendLine($"RawSpanId: {_rawSpanId}");
        sb.AppendLine($"Origin: {_origin}");
        sb.AppendLine($"ServiceName: {_serviceName}");
        sb.AppendLine($"OperationName: {_operationName}");
        sb.AppendLine($"Resource: {_resourceName}");
        sb.AppendLine($"Type: {_type}");
        sb.AppendLine($"Start: {_startTime}");
        sb.AppendLine($"Duration: {_duration}");
        sb.AppendLine($"Error: {_error}");
        sb.AppendLine($"Meta: {_tags}");

        return StringBuilderCache.GetStringAndRelease(sb);
    }
}

internal partial class SpanData : ISpanContextInternal
{
    private static readonly string[] KeyNames =
    {
        Keys.TraceId,
        Keys.ParentId,
        Keys.SamplingPriority,
        Keys.Origin,
        Keys.RawTraceId,
        Keys.RawSpanId,
        Keys.PropagatedTags,
        Keys.AdditionalW3CTraceState,

        // For mismatch version support we need to keep supporting old keys.
        HttpHeaderNames.TraceId,
        HttpHeaderNames.ParentId,
        HttpHeaderNames.SamplingPriority,
        HttpHeaderNames.Origin,
    };

    /// <summary>
    /// An <see cref="ISpanContext"/> with default values. Can be used as the value for
    /// <see cref="SpanCreationSettings.Parent"/> in <see cref="Tracer.StartActive(string, SpanCreationSettings)"/>
    /// to specify that the new span should not inherit the currently active scope as its parent.
    /// </summary>
    public static readonly ISpanContext None = new ReadOnlySpanContext(traceId: Trace.TraceId.Zero, spanId: 0, serviceName: null);

    ulong ISpanContext.TraceId => _traceId128.Lower;

    ulong ISpanContext.SpanId => _spanId;

    string ISpanContext.ServiceName => _serviceName;

    ISpanContext ISpanContextInternal.Parent => _parent;

    TraceId ISpanContextInternal.TraceId128 => _traceId128;

    ulong? ISpanContextInternal.ParentId => _parent?.SpanId;

    string ISpanContextInternal.Origin
    {
        get => _traceContext?.Origin ?? _origin;
        set
        {
            _origin = value;

            if (_traceContext is not null)
            {
                _traceContext.Origin = value;
            }
        }
    }

    TraceTagCollection ISpanContextInternal.PropagatedTags
    {
        get => _propagatedTags;
        set => _propagatedTags = value;
    }

    TraceContext ISpanContextInternal.TraceContext
    {
        get => _traceContext;
    }

    int? ISpanContextInternal.SamplingPriority
    {
        get => _samplingPriority;
    }

    string ISpanContextInternal.RawTraceId => _rawTraceId ??= HexString.ToHexString(_traceId128);

    string ISpanContextInternal.RawSpanId => _rawSpanId ??= HexString.ToHexString(_spanId);

    string ISpanContextInternal.AdditionalW3CTraceState
    {
        get => _additionalW3CTraceState;
        set => _additionalW3CTraceState = value;
    }

    PathwayContext? ISpanContextInternal.PathwayContext
    {
        get => _pathwayContext;
    }

    private static TraceId GetTraceId(ISpanContext context, TraceId fallback)
    {
        return context switch
        {
            // if there is no context or it has a zero trace id,
            // use the specified fallback value
            null or { TraceId: 0 } => fallback,

            // use the 128-bit trace id from SpanContext if possible
            SpanContext sc => sc.TraceId128,

            // otherwise use the 64-bit trace id from ISpanContext
            _ => (TraceId)context.TraceId
        };
    }

    [return: MaybeNull]
    TraceTagCollection ISpanContextInternal.PrepareTagsForPropagation()
    {
        TraceTagCollection propagatedTags;

        // use the value from TraceContext if available
        if (_traceContext != null)
        {
            propagatedTags = _traceContext.Tags;
        }
        else
        {
            if (_traceId128.Upper > 0 && _propagatedTags == null)
            {
                // we need to add the "_dd.p.tid" propagated tag, so create a new collection if we don't have one
                _propagatedTags = new TraceTagCollection();
            }

            propagatedTags = _propagatedTags;
        }

        // add, replace, or remove the "_dd.p.tid" tag
        propagatedTags?.FixTraceIdTag(_traceId128);
        return propagatedTags;
    }

    [return: MaybeNull]
    string ISpanContextInternal.PrepareTagsHeaderForPropagation()
    {
        // try to get max length from tracer settings, but do NOT access Tracer.Instance
        var headerMaxLength = _traceContext?.Tracer?.Settings?.OutgoingTagPropagationHeaderMaxLength;

        var propagatedTags = ((ISpanContextInternal)this).PrepareTagsForPropagation();
        return propagatedTags?.ToPropagationHeader(headerMaxLength);
    }

    void ISpanContextInternal.SetCheckpoint(DataStreamsManager manager, CheckpointKind checkpointKind, string[] edgeTags)
    {
        _pathwayContext = manager.SetCheckpoint(_pathwayContext, checkpointKind, edgeTags);
    }

    void ISpanContextInternal.MergePathwayContext(PathwayContext? pathwayContext)
    {
        if (pathwayContext is null)
        {
            return;
        }

        if (_pathwayContext is null)
        {
            _pathwayContext = pathwayContext;
            return;
        }

        // This is purposely not thread safe
        // The code randomly chooses between the two PathwayContexts.
        // If there is a race, then that's okay
        // Randomly select between keeping the current context (0) or replacing (1)
        if (ThreadSafeRandom.Shared.Next(2) == 1)
        {
            _pathwayContext = pathwayContext;
        }
    }

/*
    void ISpanContextInternal.SetSpanContext(ISpanContextInternal spanContext)
    {
        _pathwayContext = spanContext.PathwayContext;
        _traceContext = spanContext.TraceContext;
        _traceId128 = spanContext.TraceId128;
        _rawSpanId = spanContext.RawSpanId;
        _origin = spanContext.Origin;
        _rawTraceId = spanContext.RawTraceId;
        _additionalW3CTraceState = spanContext.AdditionalW3CTraceState;
        _serviceName = spanContext.ServiceName;
        _parent = spanContext.Parent;
        _propagatedTags = spanContext.PropagatedTags;
        _samplingPriority = spanContext.SamplingPriority;
        _spanId = spanContext.SpanId;
    }
*/

    /// <inheritdoc/>
    int IReadOnlyCollection<KeyValuePair<string, string>>.Count => KeyNames.Length;

    /// <inheritdoc />
    IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => KeyNames;

    /// <inheritdoc/>
    IEnumerable<string> IReadOnlyDictionary<string, string>.Values
    {
        get
        {
            foreach (var key in KeyNames)
            {
                yield return ((IReadOnlyDictionary<string, string>)this)[key];
            }
        }
    }

    /// <inheritdoc/>
    string IReadOnlyDictionary<string, string>.this[string key]
    {
        get
        {
            if (((IReadOnlyDictionary<string, string>)this).TryGetValue(key, out var value))
            {
                return value;
            }

            ThrowHelper.ThrowKeyNotFoundException($"Key not found: {key}");
            return default;
        }
    }

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
    {
        var dictionary = (IReadOnlyDictionary<string, string>)this;

        foreach (var key in KeyNames)
        {
            yield return new KeyValuePair<string, string>(key, dictionary[key]);
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IReadOnlyDictionary<string, string>)this).GetEnumerator();
    }

    /// <inheritdoc/>
    bool IReadOnlyDictionary<string, string>.ContainsKey(string key)
    {
        foreach (var k in KeyNames)
        {
            if (k == key)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    bool IReadOnlyDictionary<string, string>.TryGetValue(string key, out string value)
    {
        var invariant = CultureInfo.InvariantCulture;

        switch (key)
        {
            case Keys.TraceId:
            case HttpHeaderNames.TraceId:
                // use the lower 64-bits for backwards compat, truncate using TraceId128.Lower
                value = _traceId128.Lower.ToString(invariant);
                return true;

            case Keys.ParentId:
            case HttpHeaderNames.ParentId:
                // returns the 64-bit span id in decimal encoding
                value = _spanId.ToString(invariant);
                return true;

            case Keys.SamplingPriority:
            case HttpHeaderNames.SamplingPriority:
                // return the value from TraceContext if available
                var samplingPriority = _traceContext?.SamplingPriority ?? _samplingPriority;
                value = samplingPriority?.ToString(invariant);
                return true;

            case Keys.Origin:
            case HttpHeaderNames.Origin:
                value = _origin;
                return true;

            case Keys.RawTraceId:
                // returns the full 128-bit trace id in hexadecimal encoding
                value = _rawTraceId;
                return true;

            case Keys.RawSpanId:
                // returns the 64-bit span id in hexadecimal encoding
                value = _rawSpanId;
                return true;

            case Keys.PropagatedTags:
            case HttpHeaderNames.PropagatedTags:
                value = ((ISpanContextInternal)this).PrepareTagsHeaderForPropagation();
                return true;

            case Keys.AdditionalW3CTraceState:
                // return the value from TraceContext if available
                value = _traceContext?.AdditionalW3CTraceState ?? _additionalW3CTraceState;
                return true;

            default:
                value = null;
                return false;
        }
    }

    /*
    void ISpanContextInternal.SetSpanContext(TraceId traceId, string serviceName)
    {
        _traceId128 = traceId == Trace.TraceId.Zero
                         ? RandomIdGenerator.Shared.NextTraceId(useAllBits: false)
                         : traceId;

        _serviceName = serviceName;

        // Because we have a ctor as part of the public api without accepting the origin tag,
        // we need to ensure new SpanContext created by this .ctor has the CI Visibility origin
        // tag if the CI Visibility mode is running to ensure the correct propagation
        // to children spans and distributed trace.
        if (CIVisibility.IsRunning)
        {
            _origin = Ci.Tags.TestTags.CIAppTestOriginName;
        }
    }

    void ISpanContextInternal.SetSpanContext(ISpanContext parent, TraceContext traceContext, string serviceName, TraceId traceId, ulong spanId, string rawTraceId, string rawSpanId)
    {
        ((ISpanContextInternal)this).SetSpanContext(GetTraceId(parent, traceId), serviceName);

        // if 128-bit trace ids are enabled, also use full uint64 for span id,
        // otherwise keep using the legacy so-called uint63s.
        var useAllBits = traceContext?.Tracer?.Settings?.TraceId128BitGenerationEnabled ?? false;

        _spanId = spanId > 0 ? spanId : RandomIdGenerator.Shared.NextSpanId(useAllBits);
        _parent = parent;
        _traceContext = traceContext;

        if (parent is SpanData spanContext)
        {
            _rawTraceId = spanContext._rawTraceId ?? rawTraceId;
            _pathwayContext = spanContext._pathwayContext;
        }
        else if (parent is ISpanContextInternal contextInternal)
        {
            _rawTraceId = contextInternal.RawTraceId ?? rawTraceId;
            _pathwayContext = contextInternal.PathwayContext;
        }
        else
        {
            _rawTraceId = rawTraceId;
        }

        _rawSpanId = rawSpanId;
    }

    void ISpanContextInternal.SetSpanContext(TraceId traceId, ulong spanId, int? samplingPriority, string serviceName, string origin, string rawTraceId, string rawSpanId)
    {
        ((ISpanContextInternal)this).SetSpanContext(traceId, serviceName);

        _spanId = spanId;
        _samplingPriority = samplingPriority;
        _origin = origin;
        _rawTraceId = rawTraceId;
        _rawSpanId = rawSpanId;
    }

    void ISpanContextInternal.SetSpanContext(TraceId traceId, ulong spanId, int? samplingPriority, string serviceName, string origin)
    {
        ((ISpanContextInternal)this).SetSpanContext(traceId, serviceName);
        _spanId = spanId;
        _samplingPriority = samplingPriority;
        _origin = origin;
    }

    void ISpanContextInternal.SetSpanContext(ulong? traceId, ulong spanId, SamplingPriority? samplingPriority, string serviceName)
    {
        ((ISpanContextInternal)this).SetSpanContext((TraceId)(traceId ?? 0), serviceName);
        // public ctor must keep accepting legacy types:
        // - traceId: ulong? => TraceId
        // - samplingPriority: SamplingPriority? => int?
        _spanId = spanId;
        _samplingPriority = (int?)samplingPriority;
    }
*/
    internal static class Keys
    {
        private const string Prefix = "__DistributedKey-";

        public const string TraceId = $"{Prefix}TraceId";
        public const string ParentId = $"{Prefix}ParentId";
        public const string SamplingPriority = $"{Prefix}SamplingPriority";
        public const string Origin = $"{Prefix}Origin";
        public const string RawTraceId = $"{Prefix}RawTraceId";
        public const string RawSpanId = $"{Prefix}RawSpanId";
        public const string PropagatedTags = $"{Prefix}PropagatedTags";
        public const string AdditionalW3CTraceState = $"{Prefix}AdditionalW3CTraceState";
    }
}
