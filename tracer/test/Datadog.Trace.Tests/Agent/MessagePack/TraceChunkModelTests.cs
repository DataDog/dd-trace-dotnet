// <copyright file="TraceChunkModelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Agent.MessagePack;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Agent.MessagePack;

public class TraceChunkModelTests
{
    private readonly TraceContext _traceContext = new(Mock.Of<IDatadogTracer>());

    [Fact]
    public void NewTraceChunk()
    {
        var traceChunk = new TraceChunkModel();

        traceChunk.HashSetCreated.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        traceChunk.HashSetCreated.Should().BeFalse();
    }

    [Fact]
    public void DefaultTraceChunk()
    {
        TraceChunkModel traceChunk = default;

        traceChunk.HashSetCreated.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        traceChunk.HashSetCreated.Should().BeFalse();
    }

    [Fact]
    public void EmptyArray()
    {
        // ArraySegment doesn't behave the same with "new ArraySegment" vs "default",
        // so we're testing both to be sure
        var spans = new ArraySegment<Span>(Array.Empty<Span>());
        var traceChunk = new TraceChunkModel(spans, traceContext: null);

        traceChunk.HashSetCreated.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        traceChunk.HashSetCreated.Should().BeFalse();
    }

    [Fact]
    public void NewArraySegment()
    {
        // ArraySegment doesn't behave the same with "new ArraySegment" vs "default",
        // so we're testing both to be sure
        var spans = new ArraySegment<Span>();
        var traceChunk = new TraceChunkModel(spans, traceContext: null);

        traceChunk.HashSetCreated.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        traceChunk.HashSetCreated.Should().BeFalse();
    }

    [Fact]
    public void DefaultArraySegment()
    {
        ArraySegment<Span> spans = default;
        var traceChunk = new TraceChunkModel(spans, traceContext: null);

        traceChunk.HashSetCreated.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        traceChunk.HashSetCreated.Should().BeFalse();
    }

    [Fact]
    public void SmallArray_RootFirst()
    {
        var spans = new[]
                    {
                        CreateSpan(traceId: 1, spanId: 10, parentId: 5),
                        CreateSpan(traceId: 1, spanId: 100, parentId: 10),
                        CreateSpan(traceId: 1, spanId: 1000, parentId: 100),
                    };

        var traceChunk = CreateTraceChunk(spans, root: spans[0]);

        traceChunk.HashSetCreated.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse(); // first span is root and has no parent
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeTrue();
        traceChunk.ParentExistsForSpanAtIndex(2).Should().BeTrue();
        traceChunk.ParentExistsForSpanAtIndex(3).Should().BeFalse(); // index out of range

        // still no HashSet
        traceChunk.HashSetCreated.Should().BeFalse();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        var span1 = traceChunk.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();

        var span2 = traceChunk.GetSpanModel(2);
        span2.IsLocalRoot.Should().BeFalse();
        span2.IsChunkOrphan.Should().BeFalse();
        span2.IsFirstSpanInChunk.Should().BeFalse();
    }

    [Fact]
    public void SmallArray_RootMiddle()
    {
        var spans = new[]
                    {
                        CreateSpan(traceId: 1, spanId: 100, parentId: 10),
                        CreateSpan(traceId: 1, spanId: 10, parentId: 5),
                        CreateSpan(traceId: 1, spanId: 1000, parentId: 100),
                    };

        var traceChunk = CreateTraceChunk(spans, root: spans[1]);

        traceChunk.HashSetCreated.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeTrue();
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // second span is root and has no parent
        traceChunk.ParentExistsForSpanAtIndex(2).Should().BeTrue();
        traceChunk.ParentExistsForSpanAtIndex(3).Should().BeFalse(); // index out of range

        traceChunk.HashSetCreated.Should().BeFalse();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeFalse();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        var span1 = traceChunk.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeTrue();
        span1.IsChunkOrphan.Should().BeTrue();
        span1.IsFirstSpanInChunk.Should().BeFalse();

        var span2 = traceChunk.GetSpanModel(2);
        span2.IsLocalRoot.Should().BeFalse();
        span2.IsChunkOrphan.Should().BeFalse();
        span2.IsFirstSpanInChunk.Should().BeFalse();
    }

    [Fact]
    public void SmallArray_RootLast()
    {
        var spans = new[]
                    {
                        CreateSpan(traceId: 1, spanId: 100, parentId: 10),
                        CreateSpan(traceId: 1, spanId: 1000, parentId: 100),
                        CreateSpan(traceId: 1, spanId: 10, parentId: 5),
                    };

        var traceChunk = CreateTraceChunk(spans, root: spans[2]);

        traceChunk.HashSetCreated.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeTrue();
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeTrue();
        traceChunk.ParentExistsForSpanAtIndex(2).Should().BeFalse(); // third span has no parent
        traceChunk.ParentExistsForSpanAtIndex(3).Should().BeFalse(); // index out of range

        traceChunk.HashSetCreated.Should().BeFalse();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeFalse();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        var span1 = traceChunk.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();

        var span2 = traceChunk.GetSpanModel(2);
        span2.IsLocalRoot.Should().BeTrue();
        span2.IsChunkOrphan.Should().BeTrue();
        span2.IsFirstSpanInChunk.Should().BeFalse();
    }

    [Fact]
    public void SmallArray_NoRoot()
    {
        // the root span is not in the same trace chunk
        var rootSpan = CreateSpan(traceId: 1, spanId: 5, parentId: 0);

        var spans = new[]
                    {
                        CreateSpan(traceId: 1, spanId: 10, parentId: 5),
                        CreateSpan(traceId: 1, spanId: 100, parentId: 10),
                        CreateSpan(traceId: 1, spanId: 1000, parentId: 100),
                    };

        var traceChunk = CreateTraceChunk(spans, root: rootSpan);

        traceChunk.HashSetCreated.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(5);

        // local root span not found in trace chunk
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse(); // first span has no parent
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeTrue();
        traceChunk.ParentExistsForSpanAtIndex(2).Should().BeTrue();
        traceChunk.ParentExistsForSpanAtIndex(3).Should().BeFalse(); // index out of range

        traceChunk.HashSetCreated.Should().BeFalse();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        var span1 = traceChunk.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();

        var span2 = traceChunk.GetSpanModel(2);
        span2.IsLocalRoot.Should().BeFalse();
        span2.IsChunkOrphan.Should().BeFalse();
        span2.IsFirstSpanInChunk.Should().BeFalse();
    }

    [Fact]
    public void LargeArray_NoParents()
    {
        // all spans have parentId = 5, which is not found
        var spans = Enumerable.Range(10, 1000)
                              .Select(spanId => CreateSpan(traceId: 1, spanId: (ulong)spanId, parentId: 5))
                              .ToArray();

        var traceChunk = CreateTraceChunk(spans, root: spans[0]);

        // HashSet created, but not initialized until used
        traceChunk.HashSetCreated.Should().BeTrue();
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        traceChunk.HashSetInitialized.Should().BeFalse();

        for (var i = 1; i < 1000; i++)
        {
            traceChunk.ParentExistsForSpanAtIndex(i).Should().BeFalse();
        }

        // HashSet was initialized and used
        traceChunk.HashSetInitialized.Should().BeTrue();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        for (var i = 1; i < 1000; i++)
        {
            var span = traceChunk.GetSpanModel(i);
            span.IsLocalRoot.Should().BeFalse();
            span.IsChunkOrphan.Should().BeTrue();
            span.IsFirstSpanInChunk.Should().BeFalse();
        }
    }

    [Fact]
    public void LargeArray_NestedParents()
    {
        // all spans have parentId = spanId - 1, which is found for every span except the first one (parentId = 9)
        var spans = Enumerable.Range(10, 1000)
                              .Select(spanId => CreateSpan(traceId: 1, spanId: (ulong)spanId, parentId: (ulong)spanId - 1))
                              .ToArray();

        var traceChunk = CreateTraceChunk(spans, root: spans[0]);

        // HashSet created, but not initialized until used
        traceChunk.HashSetCreated.Should().BeTrue();
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        traceChunk.HashSetInitialized.Should().BeFalse();

        // the second span is a direct descendant of root span, so parent is found,
        // but HashSet is not initialized yet (not needed)
        traceChunk.ParentExistsForSpanAtIndex(1).Should().BeTrue();
        traceChunk.HashSetInitialized.Should().BeFalse();

        for (var i = 2; i < 1000; i++)
        {
            traceChunk.ParentExistsForSpanAtIndex(i).Should().BeTrue("because parent id {0} was expected in the HashSet", spans[i].Context.ParentId);
        }

        // HashSet was initialized and used for other spans
        traceChunk.HashSetInitialized.Should().BeTrue();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        for (var i = 1; i < 1000; i++)
        {
            var span = traceChunk.GetSpanModel(i);
            span.IsLocalRoot.Should().BeFalse();
            span.IsChunkOrphan.Should().BeFalse();
            span.IsFirstSpanInChunk.Should().BeFalse();
        }
    }

    [Fact]
    public void LargeArray_FlatParents()
    {
        // all spans have parentId = 5 except the first one, which has spanId = 5 and parentId = 0
        var rootSpan = CreateSpan(traceId: 1, spanId: 5, parentId: 0);
        var childSpans = Enumerable.Range(10, 1000)
                                   .Select(spanId => CreateSpan(traceId: 1, spanId: (ulong)spanId, parentId: 5));
        var spans = new[] { rootSpan }.Concat(childSpans)
                                      .ToArray();

        var traceChunk = CreateTraceChunk(spans, root: rootSpan);

        // HashSet created, but not initialized until used
        traceChunk.HashSetCreated.Should().BeTrue();
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(5);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        traceChunk.HashSetInitialized.Should().BeFalse();

        for (var i = 1; i < 1000; i++)
        {
            // all spans are direct descendants of root span, so parent is found
            // and HashSet is never initialized (not needed)
            traceChunk.ParentExistsForSpanAtIndex(i).Should().BeTrue("because parent id {0} was expected in the HashSet", spans[i].Context.ParentId);
            traceChunk.HashSetInitialized.Should().BeFalse();
        }

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        for (var i = 1; i < 1000; i++)
        {
            var span = traceChunk.GetSpanModel(i);
            span.IsLocalRoot.Should().BeFalse();
            span.IsChunkOrphan.Should().BeFalse();
            span.IsFirstSpanInChunk.Should().BeFalse();
        }
    }

    [Fact]
    public void LargeArray_NoRoot()
    {
        // the root span is not in the same trace chunk
        var rootSpan = CreateSpan(traceId: 1, spanId: 9, parentId: 0);

        // all spans have parentId = spanId - 1, which is found for every span except the first one (parentId = 9)
        var spans = Enumerable.Range(10, 1000)
                              .Select(spanId => CreateSpan(traceId: 1, spanId: (ulong)spanId, parentId: (ulong)spanId - 1))
                              .ToArray();

        var traceChunk = CreateTraceChunk(spans, root: rootSpan);

        // HashSet created, but not initialized until used
        traceChunk.HashSetCreated.Should().BeTrue();
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(9);
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        // HashSet is not used for the first span because we know its parent is the local root,
        // and that it's not in this trace chunk
        traceChunk.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        traceChunk.HashSetInitialized.Should().BeFalse();

        // HashSet is used for all the other spans
        for (var i = 2; i < 1000; i++)
        {
            traceChunk.ParentExistsForSpanAtIndex(i).Should().BeTrue("because parent id {0} was expected in the HashSet", spans[i].Context.ParentId);
            traceChunk.HashSetInitialized.Should().BeTrue();
        }
    }

    private static TraceChunkModel CreateTraceChunk(IEnumerable<Span> spans, Span root)
    {
        var spansArray = new ArraySegment<Span>(spans.ToArray());
        return new TraceChunkModel(spansArray, root);
    }

    private Span CreateSpan(ulong traceId, ulong spanId, ulong parentId)
    {
        var parentContent = new SpanContext(traceId, parentId);
        var spanContext = new SpanContext(parentContent, _traceContext, serviceName: null, spanId: spanId);
        return new Span(spanContext, DateTimeOffset.UtcNow);
    }
}
