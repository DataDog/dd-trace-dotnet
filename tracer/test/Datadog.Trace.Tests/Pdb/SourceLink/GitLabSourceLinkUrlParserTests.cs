// <copyright file="GitLabSourceLinkUrlParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Pdb.SourceLink;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Pdb.SourceLink;

public class GitLabSourceLinkUrlParserTests
{
    private const string ValidSha = "dd35903c688a74b62d1c6a9e4f41371c65704db8";

    private readonly GitLabSourceLinkUrlParser _parser = new();

    [Theory]
    [InlineData(
        "https://gitlab.com/test-org/test-repo/raw/" + ValidSha + "/*",
        ValidSha,
        "https://gitlab.com/test-org/test-repo")]
    [InlineData(
        "https://my-gitlab.example.com/org/repo/raw/" + ValidSha + "/*",
        ValidSha,
        "https://my-gitlab.example.com/org/repo")]
    [InlineData(
        "https://gitlab.example.com:8443/org/repo/raw/" + ValidSha + "/*",
        ValidSha,
        "https://gitlab.example.com:8443/org/repo")]
    public void TryParseSourceLinkUrl_ValidUrl_ReturnsTrue(string url, string expectedSha, string expectedRepoUrl)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeTrue();
        commitSha.Should().Be(expectedSha);
        repositoryUrl.Should().Be(expectedRepoUrl);
    }

    [Theory]
    [InlineData("https://gitlab.com/test-org/raw/" + ValidSha + "/*")] // too few segments (4 instead of 5)
    [InlineData("https://gitlab.com/test-org/sub/test-repo/raw/" + ValidSha + "/*")] // too many segments
    [InlineData("https://gitlab.com/test-org/test-repo/blob/" + ValidSha + "/*")] // segments[2] != "raw"
    [InlineData("https://gitlab.com/test-org/test-repo/raw/" + ValidSha + "/specific-file")] // segments[4] != "*"
    [InlineData("https://gitlab.com/test-org/test-repo/raw/invalid-sha/*")] // invalid sha
    public void TryParseSourceLinkUrl_InvalidUrl_ReturnsFalse(string url)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeFalse();
        commitSha.Should().BeNull();
        repositoryUrl.Should().BeNull();
    }
}
