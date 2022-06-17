// <copyright file="TagPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Tagging;

public class TagPropagationTests
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

    [Theory]
    [MemberData(nameof(ParseData))]
    public void ParseFromPropagationHeader(string header, KeyValuePair<string, string>[] expectedPairs)
    {
        var parsed = TagPropagation.TryParseHeader(header, 512, out var tags);

        parsed.Should().BeTrue();
        tags.ToEnumerable().Should().BeEquivalentTo(expectedPairs);
    }

    [Theory]
    [InlineData(8, 128)]   // this produces "_dd.p.a=" which is too short and invalid
    [InlineData(9, 128)]   // this produces the shortest possible header, "_dd.p.a=b"
    [InlineData(128, 128)] // this produces the longest possible header
    [InlineData(129, 128)] // this produces a header that is too long
    public void ToPropagationHeaderValue_Length(int totalHeaderLength, int maxHeaderLength)
    {
        var traceTags = new TraceTagCollection();

        // single tag with "_dd.p.a={...}", which has 8 chars plus the value's length
        traceTags.SetTag("_dd.p.a", new string('b', totalHeaderLength - 8));

        var headerValue = traceTags.ToPropagationHeader();

        if (totalHeaderLength < TagPropagation.MinimumPropagationHeaderLength)
        {
            // too short
            headerValue.Should().BeNullOrEmpty();
            traceTags.GetTag(Tags.TagPropagation.Error).Should().BeNull();
        }
        else if (totalHeaderLength > maxHeaderLength)
        {
            // too long
            headerValue.Should().BeNullOrEmpty();
            traceTags.GetTag(Tags.TagPropagation.Error).Should().Be("max_size");
        }
        else
        {
            // valid length
            headerValue.Should().NotBeNullOrEmpty();
            traceTags.GetTag(Tags.TagPropagation.Error).Should().BeNull();
        }
    }

    [Fact]
    public void HeaderIsCached()
    {
        var header = "_dd.p.key1=value1";

        // should cache original header
        var parsed = TagPropagation.TryParseHeader(header, 512, out var tags);
        parsed.Should().BeTrue();
        var cachedHeader = tags!.ToPropagationHeader();
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
        var parsed = TagPropagation.TryParseHeader(header, 512, out var tags);
        parsed.Should().BeTrue();
        var cachedHeader = tags!.ToPropagationHeader();
        cachedHeader.Should().NotBeSameAs(header);
    }
}
