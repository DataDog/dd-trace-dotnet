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
        var traceChunk = new ArraySegment<Span>(Array.Empty<Span>());
        var lookup = new SpanIdLookup(traceChunk);
        lookup.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void NewArraySegment_NotFound()
    {
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
    }

    [Fact]
    public void SmallArray_Found()
    {
        var traceChunk = GetTraceChunk(10, 100, 1000);
        var lookup = new SpanIdLookup(traceChunk);

        lookup.Contains(10).Should().BeTrue();
        lookup.Contains(100).Should().BeTrue();
        lookup.Contains(1000).Should().BeTrue();
    }

    [Fact]
    public void LargeArray_NotFound()
    {
        var traceChunk = GetTraceChunk(Enumerable.Range(10, 1000));
        var lookup = new SpanIdLookup(traceChunk);

        lookup.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void LargeArray_Found()
    {
        var traceChunk = GetTraceChunk(Enumerable.Range(10, 1000));
        var lookup = new SpanIdLookup(traceChunk);

        lookup.Contains(10).Should().BeTrue();
        lookup.Contains(100).Should().BeTrue();
        lookup.Contains(1000).Should().BeTrue();
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
