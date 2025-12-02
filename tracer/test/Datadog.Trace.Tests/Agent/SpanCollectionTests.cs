// <copyright file="SpanCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

public class SpanCollectionTests
{
    [Fact]
    public void DefaultValue()
    {
        SpanCollection collection = default;

        collection.Count.Should().Be(0);
        collection.FirstSpan.Should().BeNull();
        collection.TryGetArray().HasValue.Should().BeFalse();
        foreach (var span in collection)
        {
            Assert.Fail("We shouldn't have a span to enumerate: " + span);
        }

        FluentActions.Invoking(() => collection[0]).Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void SingleSpanConstructor()
    {
        var span = CreateSpan();
        var collection = new SpanCollection(span);

        collection.Count.Should().Be(1);
        collection.FirstSpan.Should().BeSameAs(span);
        collection.TryGetArray().HasValue.Should().BeFalse();
        var spans = new List<Span>();
        foreach (var x in collection)
        {
            spans.Add(x);
        }

        spans.Should().ContainSingle().Which.Should().BeSameAs(span);
        collection[0].Should().BeSameAs(span);
        FluentActions.Invoking(() => collection[1]).Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void ArrayCapacityConstructor()
    {
        var length = 10;
        var collection = new SpanCollection(length);

        collection.Count.Should().Be(0);
        collection.FirstSpan.Should().BeNull();
        var array = collection.TryGetArray();
        array.HasValue.Should().BeTrue();
        array!.Value.Count.Should().Be(0);
        array.Value.Offset.Should().Be(0);
        array.Value.Array!.Length.Should().Be(length);
        foreach (var span in collection)
        {
            Assert.Fail("We shouldn't have a span to enumerate: " + span);
        }

        FluentActions.Invoking(() => collection[0]).Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void ArrayConstructor()
    {
        var spans = new[] { CreateSpan("span1"), CreateSpan("span2"), null, null };
        var collection = new SpanCollection(spans, 2);

        collection.Count.Should().Be(2);
        collection.FirstSpan.Should().BeSameAs(spans[0]);
        collection[0].Should().BeSameAs(spans[0]);
        collection[1].Should().BeSameAs(spans[1]);
        FluentActions.Invoking(() => collection[2]).Should().Throw<IndexOutOfRangeException>();

        var array = collection.TryGetArray();
        array.HasValue.Should().BeTrue();
        array!.Value.Count.Should().Be(2);
        array.Value.Offset.Should().Be(0);
        array.Value.Array.Should().BeSameAs(spans);

        var enumerated = new List<Span>();
        foreach (var span in collection)
        {
            enumerated.Add(span);
        }

        enumerated.Should().HaveCount(2);
        enumerated[0].Should().BeSameAs(spans[0]);
        enumerated[1].Should().BeSameAs(spans[1]);
    }

    [Fact]
    public void ArrayConstructor_SetsCountToArrayLength()
    {
        var spans = new[] { CreateSpan("span1"), CreateSpan("span2") };
        var collection = new SpanCollection(spans);

        collection.Count.Should().Be(2);
        collection.FirstSpan.Should().BeSameAs(spans[0]);
        collection[0].Should().BeSameAs(spans[0]);
        collection[1].Should().BeSameAs(spans[1]);
    }

    [Fact]
    public void Append_ToEmptyCollection()
    {
        SpanCollection collection = default;
        var span = CreateSpan();

        var result = SpanCollection.Append(in collection, span);

        result.Count.Should().Be(1);
        result.FirstSpan.Should().BeSameAs(span);
        result[0].Should().BeSameAs(span);
    }

    [Fact]
    public void Append_ToSingleSpanCollection()
    {
        var span1 = CreateSpan("span1");
        var collection = new SpanCollection(span1);

        var span2 = CreateSpan("span2");
        var result = SpanCollection.Append(in collection, span2);

        result.Count.Should().Be(2);
        result.FirstSpan.Should().BeSameAs(span1);
        result[0].Should().BeSameAs(span1);
        result[1].Should().BeSameAs(span2);

        // Default array size is 4
        var array = result.TryGetArray();
        array.HasValue.Should().BeTrue();
        array!.Value.Count.Should().Be(2);
        array.Value.Array!.Length.Should().Be(4);
    }

    [Fact]
    public void Append_ToArrayWithCapacityCollection()
    {
        var spans = new[] { CreateSpan("span1"), CreateSpan("span2"), null, null };
        var collection = new SpanCollection(spans, 2);
        var array = collection.TryGetArray();
        array.HasValue.Should().BeTrue();
        array!.Value.Count.Should().Be(2);
        array.Value.Array.Should().BeSameAs(spans);

        var span3 = CreateSpan("span3");
        var result = SpanCollection.Append(in collection, span3);

        result.Count.Should().Be(3);
        result[0].Should().BeSameAs(spans[0]);
        result[1].Should().BeSameAs(spans[1]);
        result[2].Should().BeSameAs(span3);

        collection.TryGetArray()!.Value.Array.Should().BeSameAs(spans);
    }

    [Fact]
    public void Append_ToFullArrayCollection()
    {
        // Create a collection with an array at capacity
        var spans = new[] { CreateSpan("span1"), CreateSpan("span2") };
        var collection = new SpanCollection(spans);
        var array = collection.TryGetArray();
        array.HasValue.Should().BeTrue();
        array!.Value.Count.Should().Be(2);
        array.Value.Array.Should().BeSameAs(spans);

        var span3 = CreateSpan("span3");
        var result = SpanCollection.Append(in collection, span3);

        result.Count.Should().Be(3);
        result[0].Should().BeSameAs(spans[0]);
        result[1].Should().BeSameAs(spans[1]);
        result[2].Should().BeSameAs(span3);

        // Array should have grown from 2 to 4
        var array2 = result.TryGetArray();
        array2.HasValue.Should().BeTrue();
        array2!.Value.Array.Should().HaveCount(4).And.NotBeSameAs(spans);
    }

    [Fact]
    public void Append_GrowsArrayExponentially()
    {
        SpanCollection collection = default;

        // Append 9 spans
        for (var i = 0; i < 9; i++)
        {
            collection = SpanCollection.Append(in collection, CreateSpan($"span{i}"));
        }

        collection.Count.Should().Be(9);
        for (int i = 0; i < 9; i++)
        {
            collection[i].OperationName.Should().Be($"span{i}");
        }

        // Verify array grew: starts at 4, grows to 8, then needs to grow to 16
        var array = collection.TryGetArray();
        array.HasValue.Should().BeTrue();
        array!.Value.Array!.Length.Should().Be(16);
    }

    private static Span CreateSpan(string operationName = "test-span")
    {
        var spanContext = new SpanContext(traceId: 1UL, spanId: 2, samplingPriority: SamplingPriority.AutoKeep);
        return new Span(spanContext, DateTimeOffset.UtcNow)
        {
            OperationName = operationName
        };
    }
}
