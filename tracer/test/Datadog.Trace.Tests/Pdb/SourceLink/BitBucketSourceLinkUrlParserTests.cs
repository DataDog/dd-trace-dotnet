// <copyright file="BitBucketSourceLinkUrlParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Pdb.SourceLink;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Pdb.SourceLink;

public class BitBucketSourceLinkUrlParserTests
{
    private const string ValidSha = "dd35903c688a74b62d1c6a9e4f41371c65704db8";

    private readonly BitBucketSourceLinkUrlParser _parser = new();

    [Theory]
    [InlineData(
        "https://api.bitbucket.org/2.0/repositories/test-org/test-repo/src/" + ValidSha + "/*",
        ValidSha,
        "https://bitbucket.org/test-org/test-repo")]
    [InlineData(
        "https://api.bitbucket.org/2.0/repositories/my-org/my-repo/src/" + ValidSha + "/*",
        ValidSha,
        "https://bitbucket.org/my-org/my-repo")]
    [InlineData(
        "https://api.bitbucket.org/2.0/repositories/test-org/test-repo/src/" + ValidSha + "/extra/path/*",
        ValidSha,
        "https://bitbucket.org/test-org/test-repo")]
    public void TryParseSourceLinkUrl_ValidUrl_ReturnsTrue(string url, string expectedSha, string expectedRepoUrl)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeTrue();
        commitSha.Should().Be(expectedSha);
        repositoryUrl.Should().Be(expectedRepoUrl);
    }

    [Theory]
    [InlineData("https://bitbucket.org/2.0/repositories/test-org/test-repo/src/" + ValidSha + "/*")] // wrong base URL
    [InlineData("https://api.bitbucket.org/2.0/repositories/test-org/test-repo/src/*")] // too few segments
    [InlineData("https://api.bitbucket.org/2.0/repositories/test-org/test-repo/src/invalid-sha/*")] // invalid sha
    [InlineData("http://api.bitbucket.org/2.0/repositories/test-org/test-repo/src/" + ValidSha + "/*")] // HTTP scheme
    [InlineData("https://api.bitbucket.org/2.0/repositories/")] // missing repo segments
    public void TryParseSourceLinkUrl_InvalidUrl_ReturnsFalse(string url)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeFalse();
        commitSha.Should().BeNull();
        repositoryUrl.Should().BeNull();
    }
}
