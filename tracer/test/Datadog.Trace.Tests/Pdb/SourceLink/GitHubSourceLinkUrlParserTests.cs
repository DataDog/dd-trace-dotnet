// <copyright file="GitHubSourceLinkUrlParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Pdb.SourceLink;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Pdb.SourceLink;

public class GitHubSourceLinkUrlParserTests
{
    private const string ValidSha = "dd35903c688a74b62d1c6a9e4f41371c65704db8";

    private readonly GitHubSourceLinkUrlParser _parser = new();

    [Theory]
    [InlineData(
        "https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/" + ValidSha + "/*",
        ValidSha,
        "https://github.com/DataDog/dd-trace-dotnet")]
    [InlineData(
        "https://raw.githubusercontent.com/my-org/my-repo/" + ValidSha + "/*",
        ValidSha,
        "https://github.com/my-org/my-repo")]
    [InlineData(
        "https://raw.githubusercontent.com/some.org/some.repo-name/" + ValidSha + "/*",
        ValidSha,
        "https://github.com/some.org/some.repo-name")]
    public void TryParseSourceLinkUrl_ValidUrl_ReturnsTrue(string url, string expectedSha, string expectedRepoUrl)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeTrue();
        commitSha.Should().Be(expectedSha);
        repositoryUrl.Should().Be(expectedRepoUrl);
    }

    [Theory]
    [InlineData("https://raw.example.com/DataDog/dd-trace-dotnet/" + ValidSha + "/*")] // wrong host
    [InlineData("https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/*")] // missing sha
    [InlineData("https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/" + ValidSha + "/extra/*")] // too many segments
    [InlineData("https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/abc123/*")] // sha too short
    [InlineData("https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/zz35903c688a74b62d1c6a9e4f41371c65704db!/*")] // non-hex chars
    [InlineData("https://raw.githubusercontent.com/")] // empty path
    public void TryParseSourceLinkUrl_InvalidUrl_ReturnsFalse(string url)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeFalse();
        commitSha.Should().BeNull();
        repositoryUrl.Should().BeNull();
    }
}
