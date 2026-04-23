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
    public void Append_ToArrayWithCapacityCollection0()
    {
        var collection = new SpanCollection(arrayBuilderCapacity: 4);
        var array = collection.TryGetArray();
        array.HasValue.Should().BeTrue();
        array!.Value.Count.Should().Be(0);

        foreach (var span in collection)
        {
            Assert.Fail("We shouldn't have a span to enumerate: " + span);
        }
    }

    [Fact]
    public void Append_ToArrayWithCapacityCollection1()
    {
        var spans = new[] { CreateSpan("span1"), null, null, null };
        var collection = new SpanCollection(spans, 1);
        var array = collection.TryGetArray();
        array.HasValue.Should().BeTrue();
        array!.Value.Count.Should().Be(1);
        array.Value.Array.Should().BeSameAs(spans);

        var enumerated = new List<Span>();
        foreach (var span in collection)
        {
            enumerated.Add(span);
        }

        enumerated.Should().HaveCount(1);
        enumerated[0].Should().BeSameAs(spans[0]);
    }

    [Fact]
    public void Append_ToArrayWithCapacityCollection2()
    {
        var spans = new[] { CreateSpan("span1"), CreateSpan("span2"), null, null };
        var collection = new SpanCollection(spans, 2);
        var array = collection.TryGetArray();
        array.HasValue.Should().BeTrue();
        array!.Value.Count.Should().Be(2);
        array.Value.Array.Should().BeSameAs(spans);

        var enumerated = new List<Span>();
        foreach (var span in collection)
        {
            enumerated.Add(span);
        }

        enumerated.Should().HaveCount(2);
        enumerated[0].Should().BeSameAs(spans[0]);
        enumerated[1].Should().BeSameAs(spans[1]);

        var span3 = CreateSpan("span3");
        var result = SpanCollection.Append(in collection, span3);

        result.Count.Should().Be(3);
        result[0].Should().BeSameAs(spans[0]);
        result[1].Should().BeSameAs(spans[1]);
        result[2].Should().BeSameAs(span3);

        enumerated = new List<Span>();
        foreach (var span in result)
        {
            enumerated.Add(span);
        }

        enumerated.Should().HaveCount(3);
        enumerated[0].Should().BeSameAs(spans[0]);
        enumerated[1].Should().BeSameAs(spans[1]);
        enumerated[2].Should().BeSameAs(span3);

        result.TryGetArray()!.Value.Array.Should().BeSameAs(spans);
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

    [Fact]
    public void ContainsSpanId_DefaultCollection_ReturnsFalse()
    {
        SpanCollection collection = default;

        collection.ContainsSpanId(1, 0).Should().BeFalse();
    }

    [Fact]
    public void ContainsSpanId_SingleSpan_FindsMatch()
    {
        var span = CreateSpan(spanId: 42);
        var collection = new SpanCollection(span);

        collection.ContainsSpanId(42, 0).Should().BeTrue();
    }

    [Fact]
    public void ContainsSpanId_SingleSpan_NoMatch()
    {
        var span = CreateSpan(spanId: 42);
        var collection = new SpanCollection(span);

        collection.ContainsSpanId(99, 0).Should().BeFalse();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ContainsSpanId_SingleSpan_IgnoresStartIndex(int startIndex)
    {
        var span = CreateSpan(spanId: 42);
        var collection = new SpanCollection(span);

        // startIndex is irrelevant for single-span collections
        collection.ContainsSpanId(42, startIndex).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public void ContainsSpanId_EmptyArrayBackedCollection_ReturnsFalse(int startIndex)
    {
        var collection = new SpanCollection(arrayBuilderCapacity: 4);
        collection.ContainsSpanId(spanId: 10, startIndex).Should().BeFalse();
    }

    [Theory]
    [CombinatorialData]
    public void ContainsSpanId_Array_FindsMatch(
        [CombinatorialValues(10, 20, 30)]ulong spanToFind,
        [CombinatorialValues(0, 1, 2, 3, 4)]int spanIndex)
    {
        var collection = new SpanCollection(
            [CreateSpan(spanId: 10), CreateSpan(spanId: 20), CreateSpan(spanId: 30)]);

        collection.ContainsSpanId(spanToFind, spanIndex).Should().BeTrue();
    }

    [Fact]
    public void ContainsSpanId_Array_NoMatch()
    {
        var collection = new SpanCollection(
            [CreateSpan(spanId: 10), CreateSpan(spanId: 20), CreateSpan(spanId: 30)]);

        collection.ContainsSpanId(99, 0).Should().BeFalse();
    }

    [Fact]
    public void ContainsSpanId_ArrayWithCount_OnlySearchesWithinCount()
    {
        // Array has 4 slots but only 2 are logically populated.
        // ContainsSpanId should only search within Count, not the entire backing array.
        var collection = new SpanCollection(
            [CreateSpan(spanId: 10), CreateSpan(spanId: 20), CreateSpan(spanId: 30), CreateSpan(spanId: 40)], count: 2);

        collection.ContainsSpanId(10, 0).Should().BeTrue();
        collection.ContainsSpanId(20, 0).Should().BeTrue();
        collection.ContainsSpanId(30, 0).Should().BeFalse();
        collection.ContainsSpanId(40, 0).Should().BeFalse();
    }

    private static Span CreateSpan(string operationName = "test-span", ulong spanId = 2)
    {
        var spanContext = new SpanContext(traceId: 1UL, spanId: spanId, samplingPriority: SamplingPriority.AutoKeep);
        return TestSpanExtensions.CreateSpan(spanContext, DateTimeOffset.UtcNow, operationName: operationName);
    }
}
