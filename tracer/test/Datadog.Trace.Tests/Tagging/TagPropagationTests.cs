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
    private const int MaxParseLength = TagPropagation.IncomingPropagationHeaderMaxLength;
    private const int MaxInjectLength = TagPropagation.OutgoingPropagationHeaderMaxLength;

    [Fact]
    public void ParseHeader()
    {
        const string header = "_dd.p.key1=value1,key2=value2,_dd.p.key3=value3";

        var expectedPairs = new KeyValuePair<string, string>[]
                            {
                                new("_dd.p.key1", "value1"),
                                // "key2" is not a propagated tag
                                new("_dd.p.key3", "value3"),
                            };

        var tags = TagPropagation.ParseHeader(header);

        tags.ToEnumerable().Should().BeEquivalentTo(expectedPairs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("key1=value1,key2=value2")] // missing key prefix
    [InlineData("_dd.p.key1=,_dd.p.key2=")] // no value
    [InlineData("=value1,=value2")]         // no key
    [InlineData("=")]                       // no key or value
    [InlineData("_dd.p.key1,value")]        // no key/value separator
    [InlineData("_dd.p.=value1")]           // key too short
    public void ParseHeader_ShouldBeEmpty(string header)
    {
        var tags = TagPropagation.ParseHeader(header);

        tags.Count.Should().Be(0);

        // no error tags added in these cases
        tags.ToEnumerable().Should().BeEmpty();
    }

    // TODO: add test with invalid chars and check for tag

    [Fact]
    public void ParseHeader_TooLong()
    {
        const int maxLength = MaxParseLength;
        var header = new string('a', maxLength + 1);

        var expectedPairs = new KeyValuePair<string, string>[]
                            {
                                new(Tags.TagPropagation.Error, PropagationErrorTagValues.ExtractMaxSize)
                            };

        var tags = TagPropagation.ParseHeader(header);

        tags.ToEnumerable().Should().BeEquivalentTo(expectedPairs);
    }

    [Theory]
    [InlineData(8)]                   // this produces "_dd.p.a=" which is too short and invalid
    [InlineData(9)]                   // this produces the shortest possible header, "_dd.p.a=b"
    [InlineData(MaxInjectLength)]     // this produces the longest possible header
    [InlineData(MaxInjectLength + 1)] // this produces a header that is too long
    public void ToPropagationHeaderValue_Length(int totalHeaderLength)
    {
        // single tag with "_dd.p.a=bbb..."
        const string key = "_dd.p.a";
        var value = new string('b', totalHeaderLength - key.Length - 1);

        var traceTags = new TraceTagCollection();
        traceTags.SetTag(key, value);
        var headerValue = traceTags.ToPropagationHeader(MaxInjectLength);

        if (totalHeaderLength < TagPropagation.MinimumPropagationHeaderLength)
        {
            // too short to parse: empty header but no error tags
            headerValue.Should().BeEmpty();
            traceTags.GetTag(Tags.TagPropagation.Error).Should().BeNull();
        }
        else if (totalHeaderLength > MaxInjectLength)
        {
            // too long to parse: empty header and an error tag
            headerValue.Should().BeEmpty();
            traceTags.GetTag(Tags.TagPropagation.Error).Should().Be(PropagationErrorTagValues.InjectMaxSize);
        }
        else
        {
            // valid length: non-empty header and no error tags
            headerValue.Should().NotBeNullOrEmpty();
            traceTags.GetTag(Tags.TagPropagation.Error).Should().BeNull();
        }
    }

    [Fact]
    public void HeaderIsCached()
    {
        const string header = "_dd.p.key1=value1";

        // should cache original header
        var tags = TagPropagation.ParseHeader(header);
        tags.Should().NotBeNull();
        var cachedHeader = tags!.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().BeSameAs(header);

        // set tag to same value, should not invalidate the cached header
        tags.SetTag("_dd.p.key1", "value1");
        cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().BeSameAs(header);

        // add valid non-distributed trace tag, should not invalidate the cached header
        tags.SetTag("key2", "value2");
        cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().BeSameAs(header);

        // add valid distributed trace tag, invalidates the cached header
        tags.SetTag("_dd.p.key3", "value3");
        cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().NotBe(header);
    }

    [Fact]
    public void HeaderIsNotCached()
    {
        // one tag is missing prefix so it is ignored
        var header = "_dd.p.key1=value1,key2=value2";

        // should not cache original header
        var tags = TagPropagation.ParseHeader(header);
        tags.Should().NotBeNull();
        var cachedHeader = tags!.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().NotBeSameAs(header);
    }
}
