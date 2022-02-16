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
    private static readonly KeyValuePair<string, string>[] EmptyTags = Array.Empty<KeyValuePair<string, string>>();

    public static TheoryData<string, KeyValuePair<string, string>[]> ParseData() => new()
    {
        {
            null,
            EmptyTags
        },
        {
            string.Empty,
            EmptyTags
        },
        {
            // multiple valid tags
            "_dd.p.key1=value1,_dd.p.key2=value2,_dd.p.key3=value3",
            new KeyValuePair<string, string>[]
            {
                new("_dd.p.key1", "value1"),
                new("_dd.p.key2", "value2"),
                new("_dd.p.key3", "value3"),
            }
        },
        {
            // missing key prefix
            "key1=value1",
            EmptyTags
        },
        {
            // no value
            "_dd.p.key1=",
            EmptyTags
        },
        {
            // no key
            "=value1",
            EmptyTags
        },
        {
            // no key or value
            "=",
            EmptyTags
        },
        {
            // no separator
            "_dd.p.key1",
            EmptyTags
        },
        {
            // key too short
            "_dd.p.=value1",
            EmptyTags
        },
    };

    public static TheoryData<KeyValuePair<string, string>[], string> SerializeData() => new()
    {
        {
            EmptyTags,
            string.Empty
        },
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
            new KeyValuePair<string, string>[]
            {
                new("key1", "value1")
            },
            string.Empty
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
        var header = tags.ToPropagationHeader();
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

        var headerValue = traceTags.ToPropagationHeader();
        headerValue.Should().Be(expectedHeader);
    }

    [Theory]
    [InlineData(8)]   // this produces "_dd.p.a=" which is too short and invalid
    [InlineData(9)]   // this produces the shortest possible header, "_dd.p.a=b"
    [InlineData(512)] // this produces the longest possible header
    [InlineData(513)] // this produces a header that is too long
    public void ToPropagationHeaderValue_Length(int totalHeaderLength)
    {
        var traceTags = new TraceTagCollection();

        // single tag with "_dd.p.a={...}", which has 8 chars plus the value's length
        traceTags.SetTag("_dd.p.a", new string('b', totalHeaderLength - 8));

        var headerValue = traceTags.ToPropagationHeader();

        if (totalHeaderLength < TraceTagCollection.MinimumPropagationHeaderLength)
        {
            // too short
            headerValue.Should().BeNullOrEmpty();
            traceTags.GetTag(TraceTagNames.Propagation.PropagationHeadersError).Should().BeNull();
        }
        else if (totalHeaderLength > TraceTagCollection.MaximumPropagationHeaderLength)
        {
            // too long
            headerValue.Should().BeNullOrEmpty();
            traceTags.GetTag(TraceTagNames.Propagation.PropagationHeadersError).Should().Be("max_size");
        }
        else
        {
            // valid length
            headerValue.Should().NotBeNullOrEmpty();
            traceTags.GetTag(TraceTagNames.Propagation.PropagationHeadersError).Should().BeNull();
        }
    }

    [Theory]
    [MemberData(nameof(ParseData))]
    public void ParseFromPropagationHeader(string header, KeyValuePair<string, string>[] expectedPairs)
    {
        var tags = TraceTagCollection.ParseFromPropagationHeader(header);
        tags.Should().BeEquivalentTo(expectedPairs);
    }

    [Fact]
    public void NewCollectionIsEmpty()
    {
        var tags = new TraceTagCollection();
        var header = tags.ToPropagationHeader();
        tags.Should().BeEmpty();
        header.Should().BeEmpty();
    }

    [Fact]
    public void SetTag()
    {
        var tags = new TraceTagCollection();

        // distributed tag is set...
        tags.SetTag("_dd.p.key1", "value1");
        var value1 = tags.GetTag("_dd.p.key1");
        value1.Should().Be("value1");

        // ...and added to the header
        var header = tags.ToPropagationHeader();
        header.Should().Be("_dd.p.key1=value1");

        // non-distributed tag is set...
        tags.SetTag("key2", "value2");
        var value2 = tags.GetTag("key2");
        value2.Should().Be("value2");

        // ...but not added to the header
        header = tags.ToPropagationHeader();
        header.Should().Be("_dd.p.key1=value1");
    }

    [Fact]
    public void SetTag_MultipleTimes()
    {
        var tags = new TraceTagCollection();
        tags.SetTag("_dd.p.key1", "value1");
        tags.SetTag("_dd.p.key1", "value1");

        var value1 = tags.GetTag("_dd.p.key1");
        value1.Should().Be("value1");

        var header = tags.ToPropagationHeader();
        header.Should().Be("_dd.p.key1=value1");
    }

    [Fact]
    public void HeaderIsCached()
    {
        var header = "_dd.p.key1=value1";

        // should cache original header
        var tags = TraceTagCollection.ParseFromPropagationHeader(header);
        var cachedHeader = tags.ToPropagationHeader();
        cachedHeader.Should().BeSameAs(header);

        // set tag to same value, should not invalidate the cached header
        tags.SetTag("_dd.p.key1", "value1");
        cachedHeader = tags.ToPropagationHeader();
        cachedHeader.Should().BeSameAs(header);

        // add valid non-distributed trace tag, should not invalidate the cached header
        tags.SetTag("key2", "value2");
        cachedHeader = tags.ToPropagationHeader();
        cachedHeader.Should().BeSameAs(header);

        // add valid distributed trace tag, invalidates the cached header
        tags.SetTag("_dd.p.key3", "value3");
        cachedHeader = tags.ToPropagationHeader();
        cachedHeader.Should().NotBe(header);
    }

    [Fact]
    public void HeaderIsNotCached()
    {
        // missing prefix, tag is ignored
        var header = "key1=value1";

        // should not cache original header
        var tags = TraceTagCollection.ParseFromPropagationHeader(header);
        var cachedHeader = tags.ToPropagationHeader();
        cachedHeader.Should().NotBeSameAs(header);
    }
}
