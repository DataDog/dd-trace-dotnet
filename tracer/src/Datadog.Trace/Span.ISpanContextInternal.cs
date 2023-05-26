// <copyright file="Span.ISpanContextInternal.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace;

internal partial class Span : ISpanContextInternal
{
    /// <summary>
    /// An <see cref="ISpanContext"/> with default values. Can be used as the value for
    /// <see cref="SpanCreationSettings.Parent"/> in <see cref="Tracer.StartActive(string, SpanCreationSettings)"/>
    /// to specify that the new span should not inherit the currently active scope as its parent.
    /// </summary>
    public static readonly ISpanContext ContextNone = new ReadOnlySpanContext(traceId: Trace.TraceId.Zero, spanId: 0, serviceName: null);

    private ISpanContext _parent;
    private TraceId _traceId128;
    private ulong _spanId;
    private string _serviceName;
    private TraceContext _traceContext;
    private TraceTagCollection _propagatedTags;
    private int? _samplingPriority;
    private string _rawTraceId;
    private string _rawSpanId;
    private string _origin;
    private string _additionalW3CTraceState;
    private PathwayContext? _pathwayContext;

    /// <summary>
    /// Gets the parent context.
    /// </summary>
    ISpanContext ISpanContextInternal.Parent => _parent;

    /// <summary>
    /// Gets the 128-bit trace id.
    /// </summary>
    TraceId ISpanContextInternal.TraceId128 => _traceId128;

    /// <summary>
    /// Gets the 64-bit trace id, or the lower 64 bits of a 128-bit trace id.
    /// </summary>
    [PublicApi]
    ulong ISpanContext.TraceId => TraceId128.Lower;

    /// <summary>
    /// Gets the span id of the parent span.
    /// </summary>
    ulong? ISpanContextInternal.ParentId => _parent?.SpanId;

    /// <summary>
    /// Gets the span id.
    /// </summary>
    ulong ISpanContext.SpanId => _spanId;

    /// <summary>
    /// Gets the service name to propagate to child spans.
    /// </summary>
    string ISpanContext.ServiceName => _serviceName;

    /// <summary>
    /// Gets or sets the origin of the trace.
    /// For local contexts, this property delegates to TraceContext.Origin.
    /// This is a temporary work around because we use SpanContext
    /// for all local spans and also for propagation.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the propagated trace tags collection.
    /// </summary>
    TraceTagCollection ISpanContextInternal.PropagatedTags
    {
        get => _propagatedTags;
        set => _propagatedTags = value;
    }

    /// <summary>
    /// Gets the trace context.
    /// Returns null for contexts created from incoming propagated context.
    /// </summary>
    TraceContext ISpanContextInternal.TraceContext => _traceContext;

    /// <summary>
    /// Gets the sampling priority for contexts created from incoming propagated context.
    /// Returns null for local contexts.
    /// </summary>
    int? ISpanContextInternal.SamplingPriority => _samplingPriority;

    /// <summary>
    /// Gets the trace id as a hexadecimal string of length 32,
    /// padded with zeros to the left if needed.
    /// </summary>
    string ISpanContextInternal.RawTraceId => _rawTraceId ??= HexString.ToHexString(TraceId128);

    /// <summary>
    /// Gets the span id as a hexadecimal string of length 16,
    /// padded with zeros to the left if needed.
    /// </summary>
    string ISpanContextInternal.RawSpanId => _rawSpanId ??= HexString.ToHexString(SpanId);

    /// <summary>
    /// Gets or sets additional key/value pairs from an upstream "tracestate" W3C header that we will propagate downstream.
    /// This value will _not_ include the "dd" key, which is parsed out into other individual values
    /// (e.g. sampling priority, origin, propagates tags, etc).
    /// </summary>
    string ISpanContextInternal.AdditionalW3CTraceState
    {
        get => _additionalW3CTraceState;
        set => _additionalW3CTraceState = value;
    }

    PathwayContext? ISpanContextInternal.PathwayContext => _pathwayContext;

    [return: MaybeNull]
    TraceTagCollection ISpanContextInternal.PrepareTagsForPropagation()
    {
        TraceTagCollection propagatedTags;

        // use the value from TraceContext if available
        if (((ISpanContextInternal)this).TraceContext != null)
        {
            propagatedTags = ((ISpanContextInternal)this).TraceContext.Tags;
        }
        else
        {
            if (TraceId128.Upper > 0 && ((ISpanContextInternal)this).PropagatedTags == null)
            {
                // we need to add the "_dd.p.tid" propagated tag, so create a new collection if we don't have one
                ((ISpanContextInternal)this).PropagatedTags = new TraceTagCollection();
            }

            propagatedTags = ((ISpanContextInternal)this).PropagatedTags;
        }

        // add, replace, or remove the "_dd.p.tid" tag
        propagatedTags?.FixTraceIdTag(TraceId128);
        return propagatedTags;
    }

    [return: MaybeNull]
    string ISpanContextInternal.PrepareTagsHeaderForPropagation()
    {
        // try to get max length from tracer settings, but do NOT access Tracer.Instance
        var headerMaxLength = ((ISpanContextInternal)this).TraceContext?.Tracer?.Settings?.OutgoingTagPropagationHeaderMaxLength;

        var propagatedTags = ((ISpanContextInternal)this).PrepareTagsForPropagation();
        return propagatedTags?.ToPropagationHeader(headerMaxLength);
    }

    /// <summary>
    /// Sets a DataStreams checkpoint
    /// </summary>
    /// <param name="manager">The <see cref="DataStreamsManager"/> to use</param>
    /// <param name="checkpointKind">The type of the checkpoint</param>
    /// <param name="edgeTags">The edge tags for this checkpoint. NOTE: These MUST be sorted alphabetically</param>
    void ISpanContextInternal.SetCheckpoint(DataStreamsManager manager, CheckpointKind checkpointKind, string[] edgeTags)
    {
        _pathwayContext = manager.SetCheckpoint(((ISpanContextInternal)this).PathwayContext, checkpointKind, edgeTags);
    }

    /// <summary>
    /// Merges two DataStreams <see cref="PathwayContext"/>
    /// Should be called when a pathway context is extracted from an incoming span
    /// Used to merge contexts in a "fan in" scenario.
    /// </summary>
    void ISpanContextInternal.MergePathwayContext(PathwayContext? pathwayContext)
    {
        if (pathwayContext is null)
        {
            return;
        }

        if (((ISpanContextInternal)this).PathwayContext is null)
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
}
