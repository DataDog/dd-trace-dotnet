// <copyright file="TraceTagCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Tagging;

public class TraceTagCollectionTests
{
    private const int MaxHeaderLength = 512;

    private static readonly SerializableDictionary EmptyTags = new();

    public static TheoryData<SerializableDictionary, string> SerializeData() => new()
    {
        { EmptyTags, string.Empty },
        {
            new()
            {
                { "_dd.p.key1", "value1" },
                { "_dd.p.key2", "value2" },
                { "_dd.p.key3", "value3" },
            },
            "_dd.p.key1=value1,_dd.p.key2=value2,_dd.p.key3=value3"
        },
        {
            // missing prefix
            new() { { "key1", "value1" } }, string.Empty
        },
        {
            // missing key
            new()
            {
                { string.Empty, "value1" },
            },
            string.Empty
        },
        {
            // missing value
            new()
            {
                { "_dd.p.key1", null },
                { "_dd.p.key2", string.Empty },
            },
            string.Empty
        },
    };

    [Fact]
    public void ThrowsOnNullKey()
    {
        var tags = new TraceTagCollection();

        tags.Invoking(t => t.SetTag(null!, "value"))
            .Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToPropagationHeaderValue_Empty()
    {
        var tags = new TraceTagCollection();
        var header = tags.ToPropagationHeader(MaxHeaderLength);
        header.Should().Be(string.Empty);
    }

    [Theory]
    [MemberData(nameof(SerializeData))]
    public void ToPropagationHeaderValue(SerializableDictionary pairs, string expectedHeader)
    {
        var traceTags = new TraceTagCollection();

        foreach (var pair in pairs)
        {
            traceTags.SetTag(pair.Key, pair.Value);
        }

        var headerValue = traceTags.ToPropagationHeader(MaxHeaderLength);
        headerValue.Should().Be(expectedHeader);
    }

    [Fact]
    public void NewCollectionIsEmpty()
    {
        var tags = new TraceTagCollection();
        tags.Count.Should().Be(0);

        var enumerator = new EmptyTagEnumerator();
        tags.Enumerate(ref enumerator);
        enumerator.IsEmpty.Should().BeTrue();

        var header = tags.ToPropagationHeader(MaxHeaderLength);
        header.Should().BeEmpty();
    }

    [Fact]
    public void SetTag()
    {
        var tags = new TraceTagCollection();
        tags.Count.Should().Be(0);

        // distributed tag is set...
        tags.SetTag("_dd.p.key1", "value1");
        var value1 = tags.GetTag("_dd.p.key1");
        value1.Should().Be("value1");
        tags.Count.Should().Be(1);

        // ...and added to the header
        var header = tags.ToPropagationHeader(MaxHeaderLength);
        header.Should().Be("_dd.p.key1=value1");

        // non-distributed tag is set...
        tags.SetTag("key2", "value2");
        var value2 = tags.GetTag("key2");
        value2.Should().Be("value2");
        tags.Count.Should().Be(2);

        // ...but not added to the header
        header = tags.ToPropagationHeader(MaxHeaderLength);
        header.Should().Be("_dd.p.key1=value1");
    }

    [Fact]
    public void SetTag_MultipleTimes()
    {
        var tags = new TraceTagCollection();
        tags.Count.Should().Be(0);

        tags.SetTag("_dd.p.key1", "value1").Should().BeTrue();
        tags.Count.Should().Be(1);

        var header1 = tags.ToPropagationHeader(MaxHeaderLength);
        header1.Should().Be("_dd.p.key1=value1");

        // no-op
        tags.SetTag("_dd.p.key1", "value1").Should().BeFalse();
        tags.Count.Should().Be(1);
        tags.GetTag("_dd.p.key1").Should().Be("value1");
        tags.ToPropagationHeader(MaxHeaderLength).Should().BeSameAs(header1); // returns cached instance

        // update tag value
        tags.SetTag("_dd.p.key1", "value2").Should().BeTrue();
        tags.Count.Should().Be(1);

        tags.GetTag("_dd.p.key1").Should().Be("value2");
        tags.ToPropagationHeader(MaxHeaderLength).Should().Be("_dd.p.key1=value2");
    }

    [Fact]
    public void TryAddTag()
    {
        var tags = new TraceTagCollection();
        tags.Count.Should().Be(0);

        // add new tag
        tags.TryAddTag("_dd.p.key1", "value1").Should().BeTrue();
        tags.Count.Should().Be(1);

        // should not add or update
        tags.TryAddTag("_dd.p.key1", "value2").Should().BeFalse();
        tags.Count.Should().Be(1);

        tags.GetTag("_dd.p.key1").Should().Be("value1");
        tags.ToPropagationHeader(MaxHeaderLength).Should().Be("_dd.p.key1=value1");
    }

    [Fact]
    public void RemoveTag()
    {
        var tags = new TraceTagCollection();
        tags.TryAddTag("_dd.p.key1", "value1");
        tags.TryAddTag("_dd.p.key2", "value2");
        tags.Count.Should().Be(2);
        tags.ToPropagationHeader(MaxHeaderLength).Should().Be("_dd.p.key1=value1,_dd.p.key2=value2");

        // tag not found
        tags.RemoveTag("wrong").Should().BeFalse();
        tags.Count.Should().Be(2);

        // remove one tag
        tags.GetTag("_dd.p.key1").Should().Be("value1");
        tags.RemoveTag("_dd.p.key1").Should().BeTrue();
        tags.RemoveTag("_dd.p.key1").Should().BeFalse();
        tags.GetTag("_dd.p.key1").Should().BeNull();
        tags.Count.Should().Be(1);
        tags.ToPropagationHeader(MaxHeaderLength).Should().Be("_dd.p.key2=value2");

        // remove last tag
        tags.GetTag("_dd.p.key2").Should().Be("value2");
        tags.RemoveTag("_dd.p.key2").Should().BeTrue();
        tags.RemoveTag("_dd.p.key2").Should().BeFalse();
        tags.GetTag("_dd.p.key2").Should().BeNull();
        tags.Count.Should().Be(0);
        tags.ToPropagationHeader(MaxHeaderLength).Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(null)]               // no tag (SetTag() handles null values)
    [InlineData("")]                 // empty tag
    [InlineData(" ")]                // whitespace
    [InlineData("aabbccddeeff0011")] // value doesn't match trace id
    public void FixTraceIdTag_128_Add_Or_Replace(string tagValue)
    {
        var traceId = new TraceId(0x1234567890abcdef, 0x1122334455667788);

        var context = new SpanContext(
            traceId: traceId,
            spanId: 1UL,
            samplingPriority: SamplingPriorityValues.UserKeep,
            serviceName: null,
            origin: "rum");

        // create empty collection and add the tag (if value is not null)
        context.PropagatedTags = new TraceTagCollection();
        context.PropagatedTags.SetTag(Tags.Propagated.TraceIdUpper, tagValue);

        // call FixTraceIdTag()
        context.PropagatedTags.FixTraceIdTag(traceId);

        // if upper 64 bits are not zero, the tag should be present and have the correct value
        context.PropagatedTags.GetTag(Tags.Propagated.TraceIdUpper).Should().Be("1234567890abcdef");
    }

    [Theory]
    [InlineData(null)]               // no tag (SetTag() handles null values)
    [InlineData("")]                 // empty tag
    [InlineData(" ")]                // whitespace
    [InlineData("aabbccddeeff0011")] // value doesn't match trace id
    [InlineData("0000000000000000")] // never add all zeros (even thought they technically match the trace id)
    public void FixTraceIdTag_64_Remove_Tag(string tagValue)
    {
        var traceId = new TraceId(0, 0x1122334455667788);

        var context = new SpanContext(
            traceId: traceId,
            spanId: 1UL,
            samplingPriority: SamplingPriorityValues.UserKeep,
            serviceName: null,
            origin: "rum");

        // create empty collection and add the tag (if value is not null)
        context.PropagatedTags = new TraceTagCollection();
        context.PropagatedTags.SetTag(Tags.Propagated.TraceIdUpper, tagValue);

        // call FixTraceIdTag()
        context.PropagatedTags.FixTraceIdTag(traceId);

        // if upper 64 bits are zero, the tag should not be present in the collection
        context.PropagatedTags.GetTag(Tags.Propagated.TraceIdUpper).Should().BeNull();
    }

    private struct EmptyTagEnumerator : TraceTagCollection.ITagEnumerator
    {
        public bool IsEmpty;

        public EmptyTagEnumerator()
        {
            IsEmpty = true;
        }

        public void Next(KeyValuePair<string, string> tag) => IsEmpty = false;
    }
}
