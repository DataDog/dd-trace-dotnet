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
    [Fact]
    public void NewTraceChunk()
    {
        var traceChunk = new TraceChunkModel();

        traceChunk.SpanCount.Should().Be(0);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.Invoking(t => t.GetSpanModel(0)).Should().Throw<ArgumentOutOfRangeException>();

        traceChunk.HashSetInitialized.Should().BeFalse();
    }

    [Fact]
    public void DefaultTraceChunk()
    {
        TraceChunkModel traceChunk = default;

        traceChunk.SpanCount.Should().Be(0);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.Invoking(t => t.GetSpanModel(0)).Should().Throw<ArgumentOutOfRangeException>();

        traceChunk.HashSetInitialized.Should().BeFalse();
    }

    [Fact]
    public void EmptyArray()
    {
        // ArraySegment doesn't behave the same with "new ArraySegment" vs "default",
        // so we're testing both to be sure
        var spans = new ArraySegment<Span>(Array.Empty<Span>());
        var traceChunk = new TraceChunkModel(spans);

        traceChunk.SpanCount.Should().Be(0);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.Invoking(t => t.GetSpanModel(0)).Should().Throw<ArgumentOutOfRangeException>();

        traceChunk.HashSetInitialized.Should().BeFalse();
    }

    [Fact]
    public void NewArraySegment()
    {
        // ArraySegment doesn't behave the same with "new ArraySegment" vs "default",
        // so we're testing both to be sure
        var spans = new ArraySegment<Span>();
        var traceChunk = new TraceChunkModel(spans);

        traceChunk.SpanCount.Should().Be(0);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.Invoking(t => t.GetSpanModel(0)).Should().Throw<ArgumentOutOfRangeException>();

        traceChunk.HashSetInitialized.Should().BeFalse();
    }

    [Fact]
    public void DefaultArraySegment()
    {
        ArraySegment<Span> spans = default;
        var traceChunk = new TraceChunkModel(spans);

        traceChunk.SpanCount.Should().Be(0);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().BeNull();
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        traceChunk.Invoking(t => t.GetSpanModel(0)).Should().Throw<ArgumentOutOfRangeException>();

        traceChunk.HashSetInitialized.Should().BeFalse();
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

        traceChunk.SpanCount.Should().Be(3);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();
        span0.Span.Should().BeSameAs(spans[0]);

        var span1 = traceChunk.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();
        span1.Span.Should().BeSameAs(spans[1]);

        var span2 = traceChunk.GetSpanModel(2);
        span2.IsLocalRoot.Should().BeFalse();
        span2.IsChunkOrphan.Should().BeFalse();
        span2.IsFirstSpanInChunk.Should().BeFalse();
        span2.Span.Should().BeSameAs(spans[2]);

        traceChunk.Invoking(t => t.GetSpanModel(3)).Should().Throw<ArgumentOutOfRangeException>();
        traceChunk.HashSetInitialized.Should().BeFalse();
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

        traceChunk.SpanCount.Should().Be(3);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeFalse();
        span0.IsFirstSpanInChunk.Should().BeTrue();
        span0.Span.Should().BeSameAs(spans[0]);

        var span1 = traceChunk.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeTrue();
        span1.IsChunkOrphan.Should().BeTrue();
        span1.IsFirstSpanInChunk.Should().BeFalse();
        span1.Span.Should().BeSameAs(spans[1]);

        var span2 = traceChunk.GetSpanModel(2);
        span2.IsLocalRoot.Should().BeFalse();
        span2.IsChunkOrphan.Should().BeFalse();
        span2.IsFirstSpanInChunk.Should().BeFalse();
        span2.Span.Should().BeSameAs(spans[2]);

        traceChunk.Invoking(t => t.GetSpanModel(3)).Should().Throw<ArgumentOutOfRangeException>();
        traceChunk.HashSetInitialized.Should().BeFalse();
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

        traceChunk.SpanCount.Should().Be(3);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeFalse();
        span0.IsFirstSpanInChunk.Should().BeTrue();
        span0.Span.Should().BeSameAs(spans[0]);

        var span1 = traceChunk.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();
        span1.Span.Should().BeSameAs(spans[1]);

        var span2 = traceChunk.GetSpanModel(2);
        span2.IsLocalRoot.Should().BeTrue();
        span2.IsChunkOrphan.Should().BeTrue();
        span2.IsFirstSpanInChunk.Should().BeFalse();
        span2.Span.Should().BeSameAs(spans[2]);

        traceChunk.Invoking(t => t.GetSpanModel(3)).Should().Throw<ArgumentOutOfRangeException>();
        traceChunk.HashSetInitialized.Should().BeFalse();
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

        traceChunk.SpanCount.Should().Be(3);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(5);

        // local root span not found in trace chunk
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();
        span0.Span.Should().BeSameAs(spans[0]);

        var span1 = traceChunk.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();
        span1.Span.Should().BeSameAs(spans[1]);

        var span2 = traceChunk.GetSpanModel(2);
        span2.IsLocalRoot.Should().BeFalse();
        span2.IsChunkOrphan.Should().BeFalse();
        span2.IsFirstSpanInChunk.Should().BeFalse();
        span2.Span.Should().BeSameAs(spans[2]);

        traceChunk.Invoking(t => t.GetSpanModel(3)).Should().Throw<ArgumentOutOfRangeException>();
        traceChunk.HashSetInitialized.Should().BeFalse();
    }

    [Fact]
    public void LargeArray_NoParents()
    {
        // all spans have parentId = 5, which is not found
        var spans = Enumerable.Range(10, 1000)
                              .Select(spanId => CreateSpan(traceId: 1, spanId: (ulong)spanId, parentId: 5))
                              .ToArray();

        var traceChunk = CreateTraceChunk(spans, root: spans[0]);

        traceChunk.SpanCount.Should().Be(1000);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();
        span0.Span.Should().BeSameAs(spans[0]);

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        traceChunk.HashSetInitialized.Should().BeFalse();

        for (var i = 1; i < 1000; i++)
        {
            var span = traceChunk.GetSpanModel(i);
            span.IsLocalRoot.Should().BeFalse();
            span.IsChunkOrphan.Should().BeTrue();
            span.IsFirstSpanInChunk.Should().BeFalse();
            span.Span.Should().BeSameAs(spans[i]);
        }

        // HashSet was initialized and used for other spans
        traceChunk.HashSetInitialized.Should().BeTrue();
    }

    [Fact]
    public void LargeArray_NestedParents_WithRoot_WithUpstream()
    {
        // all spans have parentId = spanId - 1, which is found for every span except the first one (parentId = 9)
        var spans = Enumerable.Range(10, 1000)
                              .Select(spanId => CreateSpan(traceId: 1, spanId: (ulong)spanId, parentId: (ulong)spanId - 1))
                              .ToArray();

        var traceChunk = CreateTraceChunk(spans, root: spans[0]);

        traceChunk.SpanCount.Should().Be(1000);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(10);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();
        span0.Span.Should().BeSameAs(spans[0]);

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        traceChunk.HashSetInitialized.Should().BeFalse();

        var span1 = traceChunk.GetSpanModel(1);
        span1.IsLocalRoot.Should().BeFalse();
        span1.IsChunkOrphan.Should().BeFalse();
        span1.IsFirstSpanInChunk.Should().BeFalse();
        span1.Span.Should().BeSameAs(spans[1]);

        // the second span is a direct descendant of root span,
        // so parent is found and HashSet is not initialized yet (not needed)
        traceChunk.HashSetInitialized.Should().BeFalse();

        for (var i = 2; i < 1000; i++)
        {
            var span = traceChunk.GetSpanModel(i);
            span.IsLocalRoot.Should().BeFalse();
            span.IsChunkOrphan.Should().BeFalse();
            span.IsFirstSpanInChunk.Should().BeFalse();
            span.Span.Should().BeSameAs(spans[i]);
        }

        // HashSet was initialized and used for other spans
        traceChunk.HashSetInitialized.Should().BeTrue();
    }

    [Fact]
    public void LargeArray_NestedParents_WithRoot_NoUpstream()
    {
        // all spans have parentId = spanId - 1, except the first one (parentId = 0)
        var rootSpan = CreateSpan(traceId: 1, spanId: 5, parentId: 0);

        var childSpans = Enumerable.Range(10, 1000)
                                   .Select(spanId => CreateSpan(traceId: 1, spanId: (ulong)spanId, parentId: (ulong)spanId - 1))
                                   .ToArray();

        var spans = new[] { rootSpan }.Concat(childSpans).ToArray();

        var traceChunk = CreateTraceChunk(spans, root: rootSpan);

        traceChunk.SpanCount.Should().Be(1001);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(5);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();
        span0.Span.Should().BeSameAs(spans[0]);

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        traceChunk.HashSetInitialized.Should().BeFalse();

        for (var i = 1; i < 1000; i++)
        {
            var span = traceChunk.GetSpanModel(i);
            span.IsLocalRoot.Should().BeFalse();
            span.IsChunkOrphan.Should().BeFalse();
            span.IsFirstSpanInChunk.Should().BeFalse();
            span.Span.Should().BeSameAs(spans[i]);
        }

        // if chunk contains the distributed root span, we don't mark any other spans as orphans
        traceChunk.HashSetInitialized.Should().BeFalse();
    }

    [Fact]
    public void LargeArray_NestedParents_NoRoot()
    {
        // the root span is not in the same trace chunk
        var rootSpan = CreateSpan(traceId: 1, spanId: 9, parentId: 8);

        // all spans have parentId = spanId - 1,
        // parent is found for every span except the first one (parentId = 9)
        var spans = Enumerable.Range(10, 1000)
                              .Select(spanId => CreateSpan(traceId: 1, spanId: (ulong)spanId, parentId: (ulong)spanId - 1))
                              .ToArray();

        var traceChunk = CreateTraceChunk(spans, root: rootSpan);

        traceChunk.SpanCount.Should().Be(1000);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(9);
        traceChunk.ContainsLocalRootSpan.Should().BeFalse();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeFalse();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();
        span0.Span.Should().BeSameAs(spans[0]);

        // HashSet is not used for the first span because we know its parent is the local root
        // and that it's not present in this trace chunk
        traceChunk.HashSetInitialized.Should().BeFalse();

        for (var i = 1; i < 1000; i++)
        {
            var span = traceChunk.GetSpanModel(i);
            span.IsLocalRoot.Should().BeFalse();
            span.IsChunkOrphan.Should().BeFalse();
            span.IsFirstSpanInChunk.Should().BeFalse();
            span.Span.Should().BeSameAs(spans[i]);
        }

        // HashSet was initialized and used for other spans
        traceChunk.HashSetInitialized.Should().BeTrue();
    }

    [Fact]
    public void LargeArray_FlatParents()
    {
        // all spans have parentId = 5 except the first one, which has spanId = 5 and parentId = 2
        var rootSpan = CreateSpan(traceId: 1, spanId: 5, parentId: 2);

        var childSpans = Enumerable.Range(10, 1000)
                                   .Select(spanId => CreateSpan(traceId: 1, spanId: (ulong)spanId, parentId: 5));

        var spans = new[] { rootSpan }.Concat(childSpans).ToArray();

        var traceChunk = CreateTraceChunk(spans, root: rootSpan);

        traceChunk.SpanCount.Should().Be(1001);
        traceChunk.HashSetInitialized.Should().BeFalse();
        traceChunk.LocalRootSpanId.Should().Be(5);
        traceChunk.ContainsLocalRootSpan.Should().BeTrue();

        var span0 = traceChunk.GetSpanModel(0);
        span0.IsLocalRoot.Should().BeTrue();
        span0.IsChunkOrphan.Should().BeTrue();
        span0.IsFirstSpanInChunk.Should().BeTrue();
        span0.Span.Should().BeSameAs(spans[0]);

        // the first span is the root span, so HashSet is not initialized yet (not needed)
        traceChunk.HashSetInitialized.Should().BeFalse();

        for (var i = 1; i < 1000; i++)
        {
            var span = traceChunk.GetSpanModel(i);
            span.IsLocalRoot.Should().BeFalse();
            span.IsChunkOrphan.Should().BeFalse();
            span.IsFirstSpanInChunk.Should().BeFalse();
            span.Span.Should().BeSameAs(spans[i]);
        }

        // all other spans are direct descendants of root span,
        // so parent is found and HashSet is not initialized (not needed)
        traceChunk.HashSetInitialized.Should().BeFalse();
    }

    [Theory]
    [InlineData(SamplingPriorityValues.UserReject)]
    [InlineData(SamplingPriorityValues.UserKeep)]
    [InlineData(SamplingPriorityValues.AutoReject)]
    [InlineData(SamplingPriorityValues.AutoKeep)]
    public void Override_SamplingPriority_WhenPresent(int samplingPriority)
    {
        var spans = new[]
                    {
                        CreateSpan(traceId: 1, spanId: 10, parentId: 5),
                    };

        var traceChunk = new TraceChunkModel(new ArraySegment<Span>(spans), samplingPriority);

        traceChunk.SamplingPriority.Should().Be(samplingPriority);
    }

    private static TraceChunkModel CreateTraceChunk(IEnumerable<Span> spans, Span root)
    {
        var spansArray = new ArraySegment<Span>(spans.ToArray());
        return new TraceChunkModel(spansArray, root);
    }

    private Span CreateSpan(ulong traceId, ulong spanId, ulong parentId)
    {
        var parentContent = Span.CreateSpanContext(traceId, parentId);
        var traceContext = new TraceContext(Mock.Of<IDatadogTracer>());
        var spanContext = Span.CreateSpanContext(parentContent, traceContext, serviceName: null, spanId: spanId);
        return Span.CreateSpan(spanContext, DateTimeOffset.UtcNow);
    }
}
