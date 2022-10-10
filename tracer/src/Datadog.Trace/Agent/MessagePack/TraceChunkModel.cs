// <copyright file="TraceChunkModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.MessagePack;

/// <summary>
/// Contains one of more spans (a trace chunk) that belong to the same trace
/// and additional context needed for serialization.
/// </summary>
internal readonly struct TraceChunkModel
{
    // for small trace chunks, use the ArraySegment<Span> copy directly, no heap allocations
    private readonly ArraySegment<Span> _spans;

    // for large trace chunks, use a HashSet<ulong> instead of iterating the array.
    // there are 3 possible states:
    //   _hashSet == null, if we know in advance that we won't use it (small traces)
    //   _hashSet.IsValueCreated == false, if we may need it (large traces), but we haven't used it yet (it may never be used)
    //   _hashSet.IsValueCreated == true, we needed the HashSet so we initialized it
    private readonly Lazy<HashSet<ulong>>? _hashSet;

    public readonly int SpanCount;

    public readonly int? SamplingPriority = null;

    public readonly string? Environment = null;

    public readonly string? ServiceVersion = null;

    public readonly TraceTagCollection? Tags = null;

    public readonly ulong? LocalRootSpanId = null;

    public readonly bool ContainsLocalRootSpan = false;

    public readonly bool HasUpstreamService = false;

    public TraceChunkModel(in ArraySegment<Span> spans, TraceContext? traceContext)
        : this(spans, traceContext?.RootSpan)
    {
        if (traceContext is not null)
        {
            SamplingPriority = traceContext.SamplingPriority;
            Environment = traceContext.Environment;
            ServiceVersion = traceContext.ServiceVersion;
            Tags = traceContext.Tags;
        }
    }

    internal TraceChunkModel(in ArraySegment<Span> spans, Span? localRootSpan)
    {
        _spans = spans;

        // don't create the Lazy<T> instance if we know we won't use it (small traces)
        _hashSet = spans.Count > 50 ? new Lazy<HashSet<ulong>>(LazyThreadSafetyMode.None) : null;

        SpanCount = spans.Count;

        if (localRootSpan is not null)
        {
            LocalRootSpanId = localRootSpan.SpanId;

            // the local root span is almost always at the end of the chunk, so start searching at the last index.
            // skip the HashSet to avoid initializing it yet, always iterate the array of spans.
            ContainsLocalRootSpan = LocalRootSpanId is not null &&
                                    Contains((ulong)LocalRootSpanId, spans.Count - 1, ignoreHashSet: true);

            HasUpstreamService = localRootSpan.Context.ParentId is not (null or 0);
        }
    }

    // used in tests
    internal bool HashSetInitialized => _hashSet?.IsValueCreated == true && _hashSet.Value.Count > 0;

    public SpanModel GetSpanModel(int spanIndex)
    {
        if (spanIndex >= SpanCount)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(spanIndex));
        }

        var span = _spans.Array![_spans.Offset + spanIndex];

        bool isLocalRoot = span.Context.ParentId is null or 0 || span.SpanId == LocalRootSpanId;
        bool isFirstSpan = spanIndex == 0;

        // calling ParentExistsForSpanAtIndex() can be expensive, so try to short-circuit if possible
        bool isChunkOrphan;

        if (isLocalRoot)
        {
            // if this the local root span, it is also a chunk orphan
            // (we won't find a parent in this trace chunk, by definition)
            isChunkOrphan = true;
        }
        else if (ContainsLocalRootSpan && (!HasUpstreamService || LocalRootSpanId == span.Context.ParentId))
        {
            // if this trace chunk contains the _distributed_ root span (parentId == 0), don't bother
            // marking any other spans as orphans because the trace agent will always pick parentId == 0 first
            // (common case if partial flushing is disabled and there is no upstream service)
            // ...OR...
            // if the span's parent is the local root span, and we already know is it present in this trace chunk
            // (common case because most local traces only have 2 tiers and
            // spans are direct descendents of the local root, no grandchildren)
            isChunkOrphan = false;
        }
        else if (!ContainsLocalRootSpan && LocalRootSpanId == span.Context.ParentId)
        {
            // if the span's parent is the local root span, and we already know is it _not_ present in this trace chunk
            isChunkOrphan = true;
        }
        else
        {
            // call Contains() as last resort. we already checked ParentId for null or 0 above.
            isChunkOrphan = !Contains(span.Context.ParentId!.Value, spanIndex, ignoreHashSet: false);
        }

        return new SpanModel(
            span,
            this,
            isLocalRoot: isLocalRoot,
            isChunkOrphan: isChunkOrphan,
            isFirstSpanInChunk: isFirstSpan);
    }

    private bool Contains(ulong spanId, int startIndex, bool ignoreHashSet)
    {
        // for larger trace chunks, use a HashSet instead of iterating the array, but only
        // initialize it on first use. in many cases we can get away without ever using the HashSet.
        if (_hashSet is not null && !ignoreHashSet)
        {
            var hashSet = _hashSet.Value;

            if (hashSet.Count == 0)
            {
                for (var i = 0; i < SpanCount; i++)
                {
                    hashSet.Add(_spans.Array![_spans.Offset + i].SpanId);
                }
            }

            return hashSet.Contains(spanId);
        }

        // wrap around the end of the array
        if (startIndex >= SpanCount)
        {
            startIndex = 0;
        }

        // iterate over the span array starting at the specified index + 1
        for (var i = startIndex; i < SpanCount; i++)
        {
            if (spanId == _spans.Array![_spans.Offset + i].SpanId)
            {
                return true;
            }
        }

        // if not found above, wrap around to the beginning to search the rest of the array
        for (var i = 0; i < startIndex; i++)
        {
            if (spanId == _spans.Array![_spans.Offset + i].SpanId)
            {
                return true;
            }
        }

        return false;
    }
}
