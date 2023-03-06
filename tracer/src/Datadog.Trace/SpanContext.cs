// <copyright file="SpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Datadog.Trace.Ci;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Util;

namespace Datadog.Trace;

// TODO not sure if this makes a ton of sense

/// <summary>
/// The SpanContext contains all the information needed to express relationships between spans inside or outside the process boundaries.
/// </summary>
internal class SpanContext : ISpanContext, IReadOnlyDictionary<string, string>
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

    public static readonly ISpanContext None = new ReadOnlySpanContext(activity: null, serviceName: null);

    private string _origin;
    private System.Diagnostics.Activity _activity;
    private ulong? _traceId;
    private ulong? _spanId;
    private string _rawTraceId;
    private string _rawSpanId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanContext"/> class
    /// from a propagated context. <see cref="Parent"/> will be null
    /// since this is a root context locally.
    /// </summary>
    /// <param name="traceId">The propagated trace id.</param>
    /// <param name="spanId">The propagated span id.</param>
    /// <param name="samplingPriority">The propagated sampling priority.</param>
    /// <param name="serviceName">The service name to propagate to child spans.</param>
    /// <param name="origin">The propagated origin of the trace.</param>
    internal SpanContext(ulong? traceId, ulong spanId, int? samplingPriority, string serviceName, string origin)
    {
        _traceId = traceId;
        _spanId = spanId;
        ServiceName = serviceName;
        SamplingPriority = samplingPriority;
        Origin = origin;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanContext"/> class
    /// from a propagated context. <see cref="Parent"/> will be null
    /// since this is a root context locally.
    /// </summary>
    /// <param name="traceId">The propagated trace id.</param>
    /// <param name="spanId">The propagated span id.</param>
    /// <param name="samplingPriority">The propagated sampling priority.</param>
    /// <param name="serviceName">The service name to propagate to child spans.</param>
    /// <param name="origin">The propagated origin of the trace.</param>
    /// <param name="rawTraceId">The raw propagated trace id</param>
    /// <param name="rawSpanId">The raw propagated span id</param>
    internal SpanContext(ulong? traceId, ulong spanId, int? samplingPriority, string serviceName, string origin, string rawTraceId, string rawSpanId)
    {
        _traceId = traceId;
        _spanId = spanId;
        ServiceName = serviceName;
        SamplingPriority = samplingPriority;
        Origin = origin;
        _rawTraceId = rawTraceId;
        _rawSpanId = rawSpanId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanContext"/> class
    /// that is the child of the specified parent context.
    /// </summary>
    /// <param name="parent">The parent context.</param>
    /// <param name="traceContext">The trace context.</param>
    /// <param name="serviceName">The service name to propagate to child spans.</param>
    /// <param name="traceId">Override the trace id if there's no parent.</param>
    /// <param name="spanId">The propagated span id.</param>
    /// <param name="rawTraceId">Raw trace id value</param>
    /// <param name="rawSpanId">Raw span id value</param>
    internal SpanContext(ISpanContext parent, TraceContext traceContext, string serviceName, ulong? traceId = null, ulong? spanId = null, string rawTraceId = null, string rawSpanId = null)
    {
        _traceId = parent?.TraceId > 0 ? parent.TraceId : traceId;
        _spanId = spanId ?? RandomIdGenerator.Shared.NextSpanId();
        Parent = parent;
        TraceContext = traceContext;

        if (parent is SpanContext spanContext)
        {
            _rawTraceId = spanContext.RawTraceId ?? rawTraceId;
            PathwayContext = spanContext.PathwayContext;
        }
        else
        {
            _rawTraceId = rawTraceId;
        }

        _rawSpanId = rawSpanId;
    }

    internal SpanContext(System.Diagnostics.Activity activity, string serviceName)
    {
        _activity = activity;
        ServiceName = serviceName;
    }

    internal SpanContext(System.Diagnostics.Activity activity, ISpanContext parent, TraceContext traceContext, string serviceName)
    {
        _activity = activity;
        Parent = parent;
        TraceContext = traceContext;
        ServiceName = serviceName;
    }

    public ulong TraceId => GetTraceId();

    public ulong SpanId => GetSpanId();

    public string ServiceName { get; } = null;

    /// <summary>
    /// Gets or sets the header value that contains the propagated trace tags,
    /// formatted as "key1=value1,key2=value2".
    /// </summary>
    internal string PropagatedTags
    {
        get
        {
            return _activity.GetCustomProperty(nameof(PropagatedTags)).ToString();
        }

        set
        {
            _activity.SetCustomProperty(nameof(PropagatedTags), value);
        }
    }

    /// <summary>
    /// Gets or sets additional key/value pairs from an upstream "tracestate" W3C header that we will propagate downstream.
    /// This value will _not_ include the "dd" key, which is parsed out into other individual values
    /// (e.g. sampling priority, origin, propagates tags, etc).
    /// </summary>
    internal string AdditionalW3CTraceState
    {
        get
        {
            return _activity.GetCustomProperty(nameof(AdditionalW3CTraceState)).ToString();
        }

        set
        {
            _activity.SetCustomProperty(nameof(AdditionalW3CTraceState), value);
        }
    }

    /// <summary>
    /// Gets the sampling priority for contexts created from incoming propagated context.
    /// Returns null for local contexts.
    /// </summary>
    internal int? SamplingPriority { get; }

    /// <summary>
    /// Gets the trace context.
    /// Returns null for contexts created from incoming propagated context.
    /// </summary>
    internal TraceContext TraceContext { get; }

    public ulong? ParentId => Parent?.SpanId;

    public ISpanContext Parent { get; }

    /// <summary>
    /// Gets the raw traceId (to support > 64bits)
    /// </summary>
    internal string RawTraceId => _activity.TraceId.ToHexString();

    /// <summary>
    /// Gets the raw spanId
    /// </summary>
    internal string RawSpanId => _activity.SpanId.ToHexString();

    internal PathwayContext? PathwayContext { get; private set; }

    /// <summary>
    /// Gets or sets the origin of the trace.
    /// For local contexts, this property delegates to TraceContext.Origin.
    /// This is a temporary work around because we use SpanContext
    /// for all local spans and also for propagation.
    /// </summary>
    internal string Origin
    {
        get => TraceContext?.Origin ?? _origin;
        set
        {
            _origin = value;

            if (TraceContext is not null)
            {
                TraceContext.Origin = value;
            }
        }
    }

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
                value = TraceId.ToString(invariant);
                return true;

            case Keys.ParentId:
            case HttpHeaderNames.ParentId:
                value = SpanId.ToString(invariant);
                return true;

            case Keys.SamplingPriority:
            case HttpHeaderNames.SamplingPriority:
                // return the value from TraceContext if available
                var samplingPriority = TraceContext?.SamplingPriority ?? SamplingPriority;
                value = samplingPriority?.ToString(invariant);
                return true;

            case Keys.Origin:
            case HttpHeaderNames.Origin:
                value = Origin;
                return true;

            case Keys.RawTraceId:
                value = RawTraceId;
                return true;

            case Keys.RawSpanId:
                value = RawSpanId;
                return true;

            case Keys.PropagatedTags:
            case HttpHeaderNames.PropagatedTags:
                // return the value from TraceContext if available
                value = TraceContext?.Tags.ToPropagationHeader() ?? PropagatedTags;
                return true;

            case Keys.AdditionalW3CTraceState:
                // return the value from TraceContext if available
                value = TraceContext?.AdditionalW3CTraceState ?? AdditionalW3CTraceState;
                return true;

            default:
                value = null;
                return false;
        }
    }

    /// <summary>
    /// Sets a DataStreams checkpoint
    /// </summary>
    /// <param name="manager">The <see cref="DataStreamsManager"/> to use</param>
    /// <param name="edgeTags">The edge tags for this checkpoint. NOTE: These MUST be sorted alphabetically</param>
    internal void SetCheckpoint(DataStreamsManager manager, string[] edgeTags)
    {
        PathwayContext = manager.SetCheckpoint(PathwayContext, edgeTags);
    }

    /// <summary>
    /// Merges two DataStreams <see cref="PathwayContext"/>
    /// Should be called when a pathway context is extracted from an incoming span
    /// Used to merge contexts in a "fan in" scenario.
    /// </summary>
    internal void MergePathwayContext(PathwayContext? pathwayContext)
    {
        if (pathwayContext is null)
        {
            return;
        }

        if (PathwayContext is null)
        {
            PathwayContext = pathwayContext;
            return;
        }

        // This is purposely not thread safe
        // The code randomly chooses between the two PathwayContexts.
        // If there is a race, then that's okay
        // Randomly select between keeping the current context (0) or replacing (1)
        if (ThreadSafeRandom.Shared.Next(2) == 1)
        {
            PathwayContext = pathwayContext;
        }
    }

    private ulong GetTraceId()
    {
        if (_activity is null)
        {
            if (_traceId is ulong traceId)
            {
                return traceId;
            }

            return 0; // TODO
        }

        return Convert.ToUInt64(_activity.TraceId.ToHexString().Substring(16), 16);
    }

    private ulong GetSpanId()
    {
        if (_activity is null)
        {
            if (_spanId is ulong spanId)
            {
                return spanId;
            }

            return 0; // TODO
        }

        return Convert.ToUInt64(_activity.SpanId.ToHexString(), 16);
    }

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
