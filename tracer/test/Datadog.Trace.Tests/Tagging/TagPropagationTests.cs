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
                                new(Tags.TagPropagation.Error, PropagationErrorTagValues.DecodingError), // "key2" is not a propagated tag
                                new("_dd.p.key3", "value3"),
                            };

        var tags = TagPropagation.ParseHeader(header);

        tags.ToArray().Should().BeEquivalentTo(expectedPairs);
    }

    [Theory]
    [InlineData(null)]                             // null header
    [InlineData("")]                               // empty header
    [InlineData("_dd.p.upstream_services=value1")] // special case: ignore deprecated key
    public void ParseHeader_ShouldBeEmpty(string header)
    {
        var tags = TagPropagation.ParseHeader(header);

        tags.Count.Should().Be(0);

        // no error tags added in these cases
        tags.ToArray().Should().BeEmpty();
    }

    [Theory]
    [InlineData("key1=value1,key2=value2")] // missing "_dd.p." prefix
    [InlineData("_dd.p.key1=,_dd.p.key2=")] // no values
    [InlineData("=value1,=value2")]         // no keys
    [InlineData("=")]                       // no key or value
    [InlineData("_dd.p.key1")]              // no key/value separator
    [InlineData("_dd.p.key1,value")]        // no key/value separator
    [InlineData("_dd.p.=value1")]           // key too short
    [InlineData("_dd.p.key 1=value1")]      // space in key
    public void ParseHeader_InvalidChars(string header)
    {
        var expectedPairs = new KeyValuePair<string, string>[]
                            {
                                new(Tags.TagPropagation.Error, PropagationErrorTagValues.DecodingError)
                            };

        var tags = TagPropagation.ParseHeader(header);

        // the error tag should be the only tag
        tags.ToArray().Should().BeEquivalentTo(expectedPairs);
    }

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

        // the error tag should be the only tag
        tags.ToArray().Should().BeEquivalentTo(expectedPairs);
    }

    [Theory]
    [InlineData(9)]                   // this produces the shortest valid header, "_dd.p.a=b"
    [InlineData(MaxInjectLength)]     // this produces the longest valid header
    public void ToPropagationHeaderValue_ValidLength(int totalHeaderLength)
    {
        // single tag with "_dd.p.a=bbb..."
        const string key = "_dd.p.a";
        var value = new string('b', totalHeaderLength - key.Length - 1);

        var traceTags = new TraceTagCollection();
        traceTags.SetTag(key, value);
        var headerValue = traceTags.ToPropagationHeader(MaxInjectLength);

        headerValue.Should().Be($"_dd.p.a={value}");
        traceTags.GetTag(Tags.TagPropagation.Error).Should().BeNull();
    }

    [Fact]
    public void ToPropagationHeaderValue_TooShort()
    {
        // single tag with "_dd.p.a=", which is too short by 1
        var traceTags = new TraceTagCollection();
        traceTags.SetTag("_dd.p.a", string.Empty);
        var headerValue = traceTags.ToPropagationHeader(MaxInjectLength);

        // too short: empty header but no error tags
        headerValue.Should().BeEmpty();
        traceTags.GetTag(Tags.TagPropagation.Error).Should().BeNull();
    }

    [Fact]
    public void ToPropagationHeaderValue_TooLong()
    {
        // single tag with "_dd.p.a=bbb..." where value too long by 1
        const string key = "_dd.p.a";
        var value = new string('b', MaxInjectLength - key.Length);

        var traceTags = new TraceTagCollection();
        traceTags.SetTag(key, value);
        var headerValue = traceTags.ToPropagationHeader(MaxInjectLength);

        // too long: empty header and an error tag
        headerValue.Should().BeEmpty();
        traceTags.GetTag(Tags.TagPropagation.Error).Should().Be(PropagationErrorTagValues.InjectMaxSize);
    }

    [Fact]
    public void ToPropagationHeaderValue_Disabled()
    {
        var traceTags = new TraceTagCollection();
        traceTags.SetTag("_dd.p.a", "b");
        var headerValue = traceTags.ToPropagationHeader(0);

        // propagation disabled: empty header and an error tag
        headerValue.Should().BeEmpty();
        traceTags.GetTag(Tags.TagPropagation.Error).Should().Be(PropagationErrorTagValues.PropagationDisabled);
    }

    [Theory]
    [InlineData("_dd.p.key 1", "value1")] // space in key
    [InlineData("_dd.p.key,1", "value1")] // comma in key
    [InlineData("_dd.p.key=1", "value1")] // equals in key
    [InlineData("_dd.p.key1", "value,1")] // comma in value
    public void ToPropagationHeaderValue_InvalidChars(string key, string value)
    {
        var traceTags = new TraceTagCollection();
        traceTags.SetTag(key, value);
        var headerValue = traceTags.ToPropagationHeader(MaxInjectLength);

        // invalid chars: empty header and an error tag
        headerValue.Should().BeEmpty();
        traceTags.GetTag(Tags.TagPropagation.Error).Should().Be(PropagationErrorTagValues.EncodingError);
    }

    [Fact]
    public void HeaderIsCached()
    {
        const string header = "_dd.p.key1=value1";

        // should cache original header
        var tags = TagPropagation.ParseHeader(header);
        tags.Should().NotBeNull();
        var cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().BeSameAs(header);

        // set tag to same value, should not invalidate the cached header
        tags.SetTag("_dd.p.key1", "value1");
        cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().BeSameAs(header);

        // add valid non-distributed trace tag, should not invalidate the cached header
        // (only propagated tags are included in the header)
        tags.SetTag("key2", "value2");
        cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().BeSameAs(header);
    }

    [Fact]
    public void AddingTagInvalidatesCachedHeader()
    {
        const string header = "_dd.p.key1=value1";

        // should cache original header
        var tags = TagPropagation.ParseHeader(header);
        tags.Should().NotBeNull();
        var cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().BeSameAs(header);

        // add valid distributed trace tag, invalidates the cached header
        tags.SetTag("_dd.p.key2", "value2");
        cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().NotBe(header);
    }

    [Fact]
    public void UpdatingTagInvalidatesCachedHeader()
    {
        const string header = "_dd.p.key1=value1";

        // should cache original header
        var tags = TagPropagation.ParseHeader(header);
        tags.Should().NotBeNull();
        var cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().BeSameAs(header);

        // update existing trace tag, invalidates the cached header
        tags.SetTag("_dd.p.key1", "value2");
        cachedHeader = tags.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().NotBe(header);
    }

    [Fact]
    public void HeaderIsNotCached()
    {
        // one tag is missing prefix so it is ignored and header is not cached
        var header = "_dd.p.key1=value1,key2=value2";

        // should not cache original header
        var tags = TagPropagation.ParseHeader(header);
        tags.Should().NotBeNull();

        var cachedHeader = tags!.ToPropagationHeader(MaxInjectLength);
        cachedHeader.Should().NotBeSameAs(header);
    }
}
