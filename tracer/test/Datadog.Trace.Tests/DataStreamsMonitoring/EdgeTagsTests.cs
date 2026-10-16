// <copyright file="EdgeTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class EdgeTagsTests
{
    private static readonly string[] Tags = ["direction:in", "topic:topic-1", "type:kafka"];

    [Fact]
    public void GetNodeHash_MatchesHashHelper()
    {
        var nodeHashBase = HashHelper.CalculateNodeHashBase("service-1", "env-1", primaryTag: null, processTags: null, containerTagsHash: null);

        new EdgeTags(Tags).GetNodeHash(nodeHashBase).Value
                          .Should().Be(HashHelper.CalculateNodeHash(nodeHashBase, Tags).Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetNodeHash_IsTheSame_WithOrWithoutMemoization(bool memoizeNodeHash)
    {
        var base1 = HashHelper.CalculateNodeHashBase("service-1", "env-1", primaryTag: null, processTags: null, containerTagsHash: null);
        var base2 = HashHelper.CalculateNodeHashBase("service-2", "env-1", primaryTag: null, processTags: null, containerTagsHash: null);
        var edgeTags = new EdgeTags(Tags, memoizeNodeHash);

        edgeTags.GetNodeHash(base1).Value.Should().Be(HashHelper.CalculateNodeHash(base1, Tags).Value);
        edgeTags.GetNodeHash(base1).Value.Should().Be(HashHelper.CalculateNodeHash(base1, Tags).Value);
        edgeTags.GetNodeHash(base2).Value.Should().Be(HashHelper.CalculateNodeHash(base2, Tags).Value);
    }

    [Fact]
    public void GetNodeHash_ReturnsSameHash_ForSameBase()
    {
        var nodeHashBase = HashHelper.CalculateNodeHashBase("service-1", "env-1", primaryTag: null, processTags: null, containerTagsHash: null);
        var edgeTags = new EdgeTags(Tags);

        var first = edgeTags.GetNodeHash(nodeHashBase);
        var second = edgeTags.GetNodeHash(nodeHashBase);

        second.Value.Should().Be(first.Value);
    }

    [Fact]
    public void GetNodeHash_IsRecomputed_WhenBaseChanges()
    {
        var base1 = HashHelper.CalculateNodeHashBase("service-1", "env-1", primaryTag: null, processTags: null, containerTagsHash: null);
        var base2 = HashHelper.CalculateNodeHashBase("service-2", "env-1", primaryTag: null, processTags: null, containerTagsHash: null);
        var edgeTags = new EdgeTags(Tags);

        var hash1 = edgeTags.GetNodeHash(base1);
        var hash2 = edgeTags.GetNodeHash(base2);

        hash2.Value.Should().Be(HashHelper.CalculateNodeHash(base2, Tags).Value);
        hash2.Value.Should().NotBe(hash1.Value);
        edgeTags.GetNodeHash(base1).Value.Should().Be(hash1.Value);
    }
}
