// <copyright file="SpanModelBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Agent.MessagePack;

internal struct SpanModelBuilder
{
    private readonly TraceChunkModel _traceChunk;

    public readonly ulong LocalTraceRootSpanId;
    public readonly bool LocalRootExists;

    // for small trace chunks, use the ArraySegment<Span> copy directly, no heap allocations
    private readonly ArraySegment<Span> _spans;

    // for large trace chunks, use a HashSet<ulong> instead of iterating the array.
    // note that his field is NOT readonly,
    // but each copy of the same SpanModelBuilder can initialize it if needed.
    private HashSet<ulong>? _hashSet;

    public SpanModelBuilder(in TraceChunkModel traceChunk)
    {
        _traceChunk = traceChunk;
        _spans = traceChunk.Spans;
        _hashSet = null;

        LocalTraceRootSpanId = traceChunk.LocalRootSpan?.SpanId ?? 0;
        LocalRootExists = false;

        // find the local root span to optimize for the common case of a "two-level" trace,
        // i.e. a trace where all spans are direct descendants of the local root span, so there are no grandchildren,
        // this makes it very fast to determine if a span's parent (the root) is present in the same trace chunk
        if (LocalTraceRootSpanId > 0)
        {
            // the local root span is the almost always the
            // last span in the chunk, so iterate backwards
            for (var i = _spans.Offset + _spans.Count - 1; i >= 0; i--)
            {
                var span = _spans.Array![i + _spans.Offset];

                if (span.SpanId == LocalTraceRootSpanId)
                {
                    LocalRootExists = true;
                    break;
                }
            }
        }
    }

    public readonly bool HashSetCreated => _hashSet is not null;

    public bool ParentExistsForSpanAtIndex(int spanIndex)
    {
        var spanCount = _spans.Count;

        if (spanIndex > spanCount - 1)
        {
            return false;
        }

        var span = _spans.Array![_spans.Offset + spanIndex];

        if (span.SpanId == LocalTraceRootSpanId || span.Context.ParentId is null or 0)
        {
            // early exit if the span is the local root span (since it has no parent by definition)
            // or is the parent span id is not set (should never happen)
            return false;
        }

        var parentSpanId = (ulong)span.Context.ParentId;

        if (parentSpanId == LocalTraceRootSpanId)
        {
            // early exit if the span is a direct descendant of the local root span,
            // we already checked if the root span is in this trace chunk because this is a very common case
            return LocalRootExists;
        }

        // for larger trace chunks, use a HashSet instead of iterating the array,
        if (spanCount > 50)
        {
            // but only create and initialize it on first use. if all spans are children
            // of the root span (common case), we can get away without using HashSet.
            if (_hashSet is null)
            {
#if NET472_OR_GREATER || NETCOREAPP2_0_OR_GREATER
                _hashSet = new HashSet<ulong>(spanCount);
#else // NETFX < 4.7.2 || NETSTANDARD < 2.1
                _hashSet = new HashSet<ulong>();
#endif

                for (var i = 0; i < spanCount; i++)
                {
                    _hashSet.Add(_spans.Array![_spans.Offset + i].SpanId);
                }
            }

            return _hashSet.Contains(parentSpanId);
        }

        // A span's parent usually finishes after its child,
        // so if we need to iterate the array, start the search at spanIndex + 1
        // (and loop back to the beginning if needed).
        var startIndex = spanIndex < spanCount - 1 ? spanIndex + 1 : 0;

        // if we didn't create a HashSet, iterate over the span array starting at the specified index
        for (var i = startIndex; i < spanCount; i++)
        {
            if (parentSpanId == _spans.Array![_spans.Offset + i].SpanId)
            {
                return true;
            }
        }

        // if not found above, wrap around to the beginning of the array to search the rest
        for (var i = 0; i < startIndex; i++)
        {
            if (parentSpanId == _spans.Array![_spans.Offset + i].SpanId)
            {
                return true;
            }
        }

        return false;
    }

    public SpanModel GetSpanModel(int spanIndex)
    {
        var span = _spans.Array![_spans.Offset + spanIndex];

        bool isLocalRoot = span.SpanId == LocalTraceRootSpanId;
        bool isChunkOrphan = isLocalRoot || !ParentExistsForSpanAtIndex(spanIndex);
        bool isFirstSpan = spanIndex == 0;

        return new SpanModel(
            span,
            _traceChunk,
            isLocalRoot: isLocalRoot,
            isChunkOrphan: isChunkOrphan,
            isFirstSpanInChunk: isFirstSpan);
    }
}
