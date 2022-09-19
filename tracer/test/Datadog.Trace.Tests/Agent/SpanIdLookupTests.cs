// <copyright file="SpanIdLookupTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Agent.MessagePack;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

public class SpanIdLookupTests
{
    [Fact]
    public void NewLookup_NotFound()
    {
        var lookup = new SpanIdLookup();
        lookup.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void DefaultLookup_NotFound()
    {
        SpanIdLookup lookup = default;
        lookup.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void EmptyArray_NotFound()
    {
        // ArraySegment doesn't behave the same with "new ArraySegment" vs "default",
        // so we're testing both to be sure
        var traceChunk = new ArraySegment<Span>(Array.Empty<Span>());
        var lookup = new SpanIdLookup(traceChunk);
        lookup.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void NewArraySegment_NotFound()
    {
        // ArraySegment doesn't behave the same with "new ArraySegment" vs "default",
        // so we're testing both to be sure
        var traceChunk = new ArraySegment<Span>();
        var lookup = new SpanIdLookup(traceChunk);
        lookup.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void DefaultArraySegment_NotFound()
    {
        ArraySegment<Span> traceChunk = default;
        var lookup = new SpanIdLookup(traceChunk);
        lookup.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void SmallArray_NotFound()
    {
        var traceChunk = GetTraceChunk(10, 100, 1000);
        var lookup = new SpanIdLookup(traceChunk);

        lookup.Contains(1).Should().BeFalse();
        lookup.Contains(1, 0).Should().BeFalse();
        lookup.Contains(1, 1).Should().BeFalse();
        lookup.Contains(1, 2).Should().BeFalse();
    }

    [Fact]
    public void SmallArray_Found()
    {
        var traceChunk = GetTraceChunk(10, 100, 1000);
        var lookup = new SpanIdLookup(traceChunk);

        // Result should be the same with or without the start index.
        // The index is only a search optimization when searching the span array
        // since parent spans will usually appear after their child spans.
        lookup.Contains(10).Should().BeTrue();
        lookup.Contains(10, 0).Should().BeTrue();
        lookup.Contains(10, 1).Should().BeTrue();
        lookup.Contains(10, 2).Should().BeTrue();

        lookup.Contains(100).Should().BeTrue();
        lookup.Contains(100, 0).Should().BeTrue();
        lookup.Contains(100, 1).Should().BeTrue();
        lookup.Contains(100, 2).Should().BeTrue();

        // The index is ignored when using the HashSet.
        lookup.Contains(1000).Should().BeTrue();
        lookup.Contains(1000, 0).Should().BeTrue();
        lookup.Contains(1000, 1).Should().BeTrue();
        lookup.Contains(1000, 2).Should().BeTrue();
    }

    [Fact]
    public void LargeArray_NotFound()
    {
        var traceChunk = GetTraceChunk(Enumerable.Range(10, 1000));
        var lookup = new SpanIdLookup(traceChunk);

        lookup.Contains(1).Should().BeFalse();
        lookup.Contains(1, 0).Should().BeFalse();
        lookup.Contains(1, traceChunk.Count / 2).Should().BeFalse();
        lookup.Contains(1, traceChunk.Count - 1).Should().BeFalse();

        lookup.Contains(2000).Should().BeFalse();
        lookup.Contains(2000, 0).Should().BeFalse();
        lookup.Contains(2000, traceChunk.Count / 2).Should().BeFalse();
        lookup.Contains(2000, traceChunk.Count - 1).Should().BeFalse();
    }

    [Fact]
    public void LargeArray_Found()
    {
        var traceChunk = GetTraceChunk(Enumerable.Range(10, 1000));
        var lookup = new SpanIdLookup(traceChunk);

        lookup.Contains(10).Should().BeTrue();
        lookup.Contains(10, 0).Should().BeTrue();
        lookup.Contains(10, traceChunk.Count / 2).Should().BeTrue();
        lookup.Contains(10, traceChunk.Count - 1).Should().BeTrue();

        lookup.Contains(100).Should().BeTrue();
        lookup.Contains(100, 0).Should().BeTrue();
        lookup.Contains(100, traceChunk.Count / 2).Should().BeTrue();
        lookup.Contains(100, traceChunk.Count - 1).Should().BeTrue();

        lookup.Contains(1000).Should().BeTrue();
        lookup.Contains(1000, 0).Should().BeTrue();
        lookup.Contains(1000, traceChunk.Count / 2).Should().BeTrue();
        lookup.Contains(1000, traceChunk.Count - 1).Should().BeTrue();
    }

    private static ArraySegment<Span> GetTraceChunk(params int[] spanIds)
    {
        return GetTraceChunk(spanIds.AsEnumerable());
    }

    private static ArraySegment<Span> GetTraceChunk(IEnumerable<int> spanIds)
    {
        var now = DateTimeOffset.UtcNow;

        var spans = from spanId in spanIds
                    let spanContext = new SpanContext(1, (ulong)spanId)
                    select new Span(spanContext, now);

        return new ArraySegment<Span>(spans.ToArray());
    }
}
