// <copyright file="TraceTagsCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Tagging;

public class TraceTagsCollectionTests
{
    private const int MaxHeaderLength = 512;

    private static readonly KeyValuePair<string, string>[] EmptyTags = Array.Empty<KeyValuePair<string, string>>();

    public static TheoryData<KeyValuePair<string, string>[], string> SerializeData() => new()
    {
        { EmptyTags, string.Empty },
        {
            new KeyValuePair<string, string>[]
            {
                new("_dd.p.key1", "value1"),
                new("_dd.p.key2", "value2"),
                new("_dd.p.key3", "value3"),
            },
            "_dd.p.key1=value1,_dd.p.key2=value2,_dd.p.key3=value3"
        },
        {
            // missing prefix
            new KeyValuePair<string, string>[] { new("key1", "value1") }, string.Empty
        },
        {
            // missing key
            new KeyValuePair<string, string>[]
            {
                new(string.Empty, "value1"),
            },
            string.Empty
        },
        {
            // missing value
            new KeyValuePair<string, string>[]
            {
                new("_dd.p.key1", null),
                new("_dd.p.key2", string.Empty),
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
    public void ToPropagationHeaderValue(KeyValuePair<string, string>[] pairs, string expectedHeader)
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
        tags.ToArray().Should().BeEmpty();

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
}
