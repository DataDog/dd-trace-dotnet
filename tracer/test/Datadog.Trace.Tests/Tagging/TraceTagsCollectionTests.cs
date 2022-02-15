// <copyright file="TraceTagsCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Tagging;

public class TraceTagsCollectionTests
{
    public static TheoryData<string, List<KeyValuePair<string, string>>> ParseData() => new()
    {
        {
            null,
            new List<KeyValuePair<string, string>>()
        },
        {
            string.Empty,
            new List<KeyValuePair<string, string>>()
        },
        {
            "a=b",
            new List<KeyValuePair<string, string>>
            {
                new("a", "b")
            }
        },
        {
            "key1=value1,key2=value2,key3=value3",
            new List<KeyValuePair<string, string>>
            {
                new("key1", "value1"),
                new("key2", "value2"),
                new("key3", "value3")
            }
        },
        {
            "key1=,=value2,=,key3",
            new List<KeyValuePair<string, string>>()
        }
    };

    public static TheoryData<List<KeyValuePair<string, string>>, string> SerializeData() => new()
    {
        {
            new List<KeyValuePair<string, string>>(),
            string.Empty
        },
        {
            new List<KeyValuePair<string, string>>
            {
                new("_dd.p.key1", "value1")
            },
            "_dd.p.key1=value1"
        },
        {
            new List<KeyValuePair<string, string>>
            {
                new("_dd.p.key1", "value1"),
                new("_dd.p.key2", "value2")
            },
            "_dd.p.key1=value1,_dd.p.key2=value2"
        },
        {
            new List<KeyValuePair<string, string>>
            {
                new("key1", "value1"),
                new("_dd.p.key2", "value2"),
                new("key3", "value3")
            },
            "_dd.p.key2=value2"
        }
    };

    [Fact]
    public void ToPropagationHeaderValue_Empty()
    {
        var tags = new TraceTagCollection();
        var header = tags.ToPropagationHeader();
        header.Should().Be(string.Empty);
    }

    [Theory]
    [MemberData(nameof(SerializeData))]
    public void ToPropagationHeaderValue(List<KeyValuePair<string, string>> pairs, string expectedHeader)
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
    public void ParseFromPropagationHeader(string header, List<KeyValuePair<string, string>> expectedPairs)
    {
        var tags = TraceTagCollection.ParseFromPropagationHeader(header).AsList();
        tags.Should().BeEquivalentTo(expectedPairs);
    }
}
