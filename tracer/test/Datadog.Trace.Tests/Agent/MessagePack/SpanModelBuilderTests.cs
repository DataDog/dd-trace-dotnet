// <copyright file="SpanModelBuilderTests.cs" company="Datadog">
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

public class SpanModelBuilderTests
{
    private readonly TraceContext _traceContext = new(Mock.Of<IDatadogTracer>());

    [Fact]
    public void NewBuilder()
    {
        var builder = new SpanModelBuilder();

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(0);
        builder.LocalRootExists.Should().BeFalse();

        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        builder.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        builder.HashSetCreated.Should().BeFalse();
    }

    [Fact]
    public void DefaultBuilder()
    {
        SpanModelBuilder builder = default;

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(0);
        builder.LocalRootExists.Should().BeFalse();

        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        builder.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        builder.HashSetCreated.Should().BeFalse();
    }

    [Fact]
    public void EmptyArray()
    {
        // ArraySegment doesn't behave the same with "new ArraySegment" vs "default",
        // so we're testing both to be sure
        var traceChunk = new ArraySegment<Span>(Array.Empty<Span>());
        var traceChunkModel = new TraceChunkModel(traceChunk, traceContext: null);
        var builder = new SpanModelBuilder(traceChunkModel);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(0);
        builder.LocalRootExists.Should().BeFalse();

        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        builder.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        builder.HashSetCreated.Should().BeFalse();
    }

    [Fact]
    public void NewArraySegment()
    {
        // ArraySegment doesn't behave the same with "new ArraySegment" vs "default",
        // so we're testing both to be sure
        var traceChunk = new ArraySegment<Span>();
        var traceChunkModel = new TraceChunkModel(traceChunk, traceContext: null);
        var builder = new SpanModelBuilder(traceChunkModel);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(0);
        builder.LocalRootExists.Should().BeFalse();

        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        builder.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        builder.HashSetCreated.Should().BeFalse();
    }

    [Fact]
    public void DefaultArraySegment()
    {
        ArraySegment<Span> traceChunk = default;
        var traceChunkModel = new TraceChunkModel(traceChunk, traceContext: null);
        var builder = new SpanModelBuilder(traceChunkModel);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(0);
        builder.LocalRootExists.Should().BeFalse();

        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        builder.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // index out of range

        builder.HashSetCreated.Should().BeFalse();
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
        var builder = new SpanModelBuilder(traceChunk);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(10);
        builder.LocalRootExists.Should().BeTrue();

        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse(); // first span is root and has no parent
        builder.ParentExistsForSpanAtIndex(1).Should().BeTrue();
        builder.ParentExistsForSpanAtIndex(2).Should().BeTrue();
        builder.ParentExistsForSpanAtIndex(3).Should().BeFalse(); // index out of range

        // still no HashSet
        builder.HashSetCreated.Should().BeFalse();

        var span0 = builder.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        var span1 = builder.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();

        var span2 = builder.GetSpanModel(2);
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
        var builder = new SpanModelBuilder(traceChunk);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(10);
        builder.LocalRootExists.Should().BeTrue();

        builder.ParentExistsForSpanAtIndex(0).Should().BeTrue();
        builder.ParentExistsForSpanAtIndex(1).Should().BeFalse(); // second span is root and has no parent
        builder.ParentExistsForSpanAtIndex(2).Should().BeTrue();
        builder.ParentExistsForSpanAtIndex(3).Should().BeFalse(); // index out of range

        builder.HashSetCreated.Should().BeFalse();

        var span0 = builder.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeFalse();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        var span1 = builder.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeTrue();
        span1.IsChunkOrphan.Should().BeTrue();
        span1.IsFirstSpanInChunk.Should().BeFalse();

        var span2 = builder.GetSpanModel(2);
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
        var builder = new SpanModelBuilder(traceChunk);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(10);
        builder.LocalRootExists.Should().BeTrue();

        builder.ParentExistsForSpanAtIndex(0).Should().BeTrue();
        builder.ParentExistsForSpanAtIndex(1).Should().BeTrue();
        builder.ParentExistsForSpanAtIndex(2).Should().BeFalse(); // third span has no parent
        builder.ParentExistsForSpanAtIndex(3).Should().BeFalse(); // index out of range

        builder.HashSetCreated.Should().BeFalse();

        var span0 = builder.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeFalse();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        var span1 = builder.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();

        var span2 = builder.GetSpanModel(2);
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
        var builder = new SpanModelBuilder(traceChunk);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(5);

        // local root span not found in trace chunk
        builder.LocalRootExists.Should().BeFalse();

        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse(); // first span has no parent
        builder.ParentExistsForSpanAtIndex(1).Should().BeTrue();
        builder.ParentExistsForSpanAtIndex(2).Should().BeTrue();
        builder.ParentExistsForSpanAtIndex(3).Should().BeFalse(); // index out of range

        builder.HashSetCreated.Should().BeFalse();

        var span0 = builder.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        var span1 = builder.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();

        var span2 = builder.GetSpanModel(2);
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
        var builder = new SpanModelBuilder(traceChunk);

        // HashSet not initialized until used
        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(10);
        builder.LocalRootExists.Should().BeTrue();

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        builder.HashSetCreated.Should().BeFalse();

        for (var i = 1; i < 1000; i++)
        {
            builder.ParentExistsForSpanAtIndex(i).Should().BeFalse();
        }

        // HashSet was initialized and used
        builder.HashSetCreated.Should().BeTrue();

        var span0 = builder.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        for (var i = 1; i < 1000; i++)
        {
            var span = builder.GetSpanModel(i);
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
        var builder = new SpanModelBuilder(traceChunk);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(10);
        builder.LocalRootExists.Should().BeTrue();

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        builder.HashSetCreated.Should().BeFalse();

        // the second span is a direct descendant of root span, so parent is found,
        // but HashSet is not initialized yet (not needed)
        builder.ParentExistsForSpanAtIndex(1).Should().BeTrue();
        builder.HashSetCreated.Should().BeFalse();

        for (var i = 2; i < 1000; i++)
        {
            builder.ParentExistsForSpanAtIndex(i).Should().BeTrue("because parent id {0} was expected in the HashSet", spans[i].Context.ParentId);
        }

        // HashSet was initialized and used for other spans
        builder.HashSetCreated.Should().BeTrue();

        var span0 = builder.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        for (var i = 1; i < 1000; i++)
        {
            var span = builder.GetSpanModel(i);
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
        var builder = new SpanModelBuilder(traceChunk);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(5);
        builder.LocalRootExists.Should().BeTrue();

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        builder.HashSetCreated.Should().BeFalse();

        for (var i = 1; i < 1000; i++)
        {
            // all spans are direct descendants of root span, so parent is found
            // and HashSet is never initialized (not needed)
            builder.ParentExistsForSpanAtIndex(i).Should().BeTrue("because parent id {0} was expected in the HashSet", spans[i].Context.ParentId);
            builder.HashSetCreated.Should().BeFalse();
        }

        var span0 = builder.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();

        for (var i = 1; i < 1000; i++)
        {
            var span = builder.GetSpanModel(i);
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
        var builder = new SpanModelBuilder(traceChunk);

        builder.HashSetCreated.Should().BeFalse();
        builder.LocalTraceRootSpanId.Should().Be(9);
        builder.LocalRootExists.Should().BeFalse();

        // HashSet is not used for the first span because we know its parent is the local root,
        // and that it's not in this trace chunk
        builder.ParentExistsForSpanAtIndex(0).Should().BeFalse();
        builder.HashSetCreated.Should().BeFalse();

        // HashSet is used for all the other spans
        for (var i = 2; i < 1000; i++)
        {
            builder.ParentExistsForSpanAtIndex(i).Should().BeTrue("because parent id {0} was expected in the HashSet", spans[i].Context.ParentId);
            builder.HashSetCreated.Should().BeTrue();
        }
    }

    private Span CreateSpan(ulong traceId, ulong spanId, ulong parentId)
    {
        var parentContent = new SpanContext(traceId, parentId);
        var spanContext = new SpanContext(parentContent, _traceContext, serviceName: null, spanId: spanId);
        return new Span(spanContext, DateTimeOffset.UtcNow);
    }

    private TraceChunkModel CreateTraceChunk(IEnumerable<Span> spans, Span root)
    {
        var traceChunk = new ArraySegment<Span>(spans.ToArray());
        return new TraceChunkModel(traceChunk, root);
    }
}
