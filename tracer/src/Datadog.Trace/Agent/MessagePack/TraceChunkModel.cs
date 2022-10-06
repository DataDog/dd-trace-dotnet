// <copyright file="TraceChunkModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Tagging;

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
    private readonly HashSet<ulong>? _hashSet;

    public readonly int SpanCount;

    public readonly int? SamplingPriority;

    public readonly TraceTagCollection? Tags;

    public readonly ulong? LocalRootSpanId;

    public readonly bool ContainsLocalRootSpan;

    public TraceChunkModel(in ArraySegment<Span> spans, TraceContext? traceContext)
    {
        _spans = spans;
        _hashSet = spans.Count > 50 ? _hashSet = CreateHashSet(spans.Count) : null;

        SpanCount = spans.Count;

        if (traceContext is null)
        {
            SamplingPriority = null;
            Tags = null;
            LocalRootSpanId = null;
            ContainsLocalRootSpan = false;
        }
        else
        {
            SamplingPriority = traceContext.SamplingPriority;
            Tags = traceContext.Tags;
            LocalRootSpanId = traceContext.RootSpan?.SpanId;
            ContainsLocalRootSpan = Contains(spans, traceContext.RootSpan);
        }
    }

    public TraceChunkModel(in ArraySegment<Span> spans, Span? localRootSpan)
    {
        _spans = spans;
        _hashSet = spans.Count > 50 ? _hashSet = CreateHashSet(spans.Count) : null;

        SpanCount = spans.Count;
        SamplingPriority = null;
        Tags = null;
        LocalRootSpanId = localRootSpan?.SpanId;
        ContainsLocalRootSpan = Contains(spans, localRootSpan);
    }

    // used in tests
    internal bool HashSetCreated => _hashSet is not null;

    // used in tests
    internal bool HashSetInitialized => _hashSet?.Count > 0;

    private static HashSet<ulong> CreateHashSet(int capacity)
    {
#if NET472_OR_GREATER || NETCOREAPP2_0_OR_GREATER
        return new HashSet<ulong>(capacity);
#else // NETFX < 4.7.2 || NETSTANDARD < 2.1
        return new HashSet<ulong>();
#endif
    }

    private static bool Contains(ArraySegment<Span> spans, Span? span)
    {
        if (span == null!)
        {
            return false;
        }

        // the local root span is almost always the
        // last span in the chunk, so iterate backwards
        for (var i = spans.Offset + spans.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(span, spans.Array![spans.Offset + i]))
            {
                return true;
            }
        }

        return false;
    }

    public SpanModel GetSpanModel(int spanIndex)
    {
        var span = _spans.Array![_spans.Offset + spanIndex];

        bool isLocalRoot = span.SpanId == LocalRootSpanId;
        bool isChunkOrphan = isLocalRoot || !ParentExistsForSpanAtIndex(spanIndex);
        bool isFirstSpan = spanIndex == 0;

        return new SpanModel(
            span,
            this,
            isLocalRoot: isLocalRoot,
            isChunkOrphan: isChunkOrphan,
            isFirstSpanInChunk: isFirstSpan);
    }

    public bool ParentExistsForSpanAtIndex(int spanIndex)
    {
        var spanCount = _spans.Count;

        if (spanIndex > spanCount - 1)
        {
            return false;
        }

        var span = _spans.Array![_spans.Offset + spanIndex];

        if (span.SpanId == LocalRootSpanId || span.Context.ParentId is null or 0)
        {
            // early exit if the span is the local root span (since it has no parent by definition)
            // or if the parent span id is otherwise not set (should never happen)
            return false;
        }

        var parentSpanId = (ulong)span.Context.ParentId;

        if (parentSpanId == LocalRootSpanId)
        {
            // early exit if the span is a direct descendant of the local root span (common case)
            return ContainsLocalRootSpan;
        }

        // for larger trace chunks, use a HashSet instead of iterating the array,
        // but only initialize it on first use. if all spans are children
        // of the root span (common case), we can get away without using HashSet.
        if (_hashSet is not null)
        {
            if (_hashSet.Count == 0)
            {
                for (var i = 0; i < spanCount; i++)
                {
                    _hashSet.Add(_spans.Array![_spans.Offset + i].SpanId);
                }
            }

            return _hashSet.Contains(parentSpanId);
        }

        // A span's parent usually finishes after its child,
        // so if we need to iterate the array, start the search at spanIndex + 1
        // (and wrap back to the beginning if needed).
        var startIndex = spanIndex < spanCount - 1 ? spanIndex + 1 : 0;

        // iterate over the span array starting at the specified index + 1
        for (var i = startIndex; i < spanCount; i++)
        {
            if (parentSpanId == _spans.Array![_spans.Offset + i].SpanId)
            {
                return true;
            }
        }

        // if not found above, wrap around to the beginning to search the rest of the array
        for (var i = 0; i < startIndex; i++)
        {
            if (parentSpanId == _spans.Array![_spans.Offset + i].SpanId)
            {
                return true;
            }
        }

        return false;
    }
}
