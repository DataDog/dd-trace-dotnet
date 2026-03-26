// <copyright file="CompositeSourceLinkUrlParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Pdb.SourceLink;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Pdb.SourceLink;

public class CompositeSourceLinkUrlParserTests
{
    private const string ValidSha = "dd35903c688a74b62d1c6a9e4f41371c65704db8";

    [Theory]
    [InlineData(
        "https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/" + ValidSha + "/*",
        ValidSha,
        "https://github.com/DataDog/dd-trace-dotnet")]
    [InlineData(
        "https://api.bitbucket.org/2.0/repositories/test-org/test-repo/src/" + ValidSha + "/*",
        ValidSha,
        "https://bitbucket.org/test-org/test-repo")]
    [InlineData(
        "https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&versionType=commit&version=" + ValidSha + "&path=/*",
        ValidSha,
        "https://test.visualstudio.com/test-org/_git/my-repo")]
    [InlineData(
        "https://dev.azure.com/org/proj/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=" + ValidSha + "&path=/*",
        ValidSha,
        "https://dev.azure.com/org/proj/_git/repo")]
    [InlineData(
        "https://gitlab.com/test-org/test-repo/raw/" + ValidSha + "/*",
        ValidSha,
        "https://gitlab.com/test-org/test-repo")]
    public void TryParseSourceLinkUrl_ValidUrl_RoutesToCorrectParser(string url, string expectedSha, string expectedRepoUrl)
    {
        var result = CompositeSourceLinkUrlParser.Instance.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeTrue();
        commitSha.Should().Be(expectedSha);
        repositoryUrl.Should().Be(expectedRepoUrl);
    }

    [Theory]
    [InlineData("https://example.com/something")] // completely unrelated
    [InlineData("https://example.com/")] // minimal path
    [InlineData("https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/invalid-sha/*")] // partially matches GitHub but invalid
    [InlineData("https://api.bitbucket.org/2.0/repositories/test-org/test-repo/src/invalid/*")] // partially matches BitBucket but invalid
    public void TryParseSourceLinkUrl_InvalidUrl_ReturnsFalse(string url)
    {
        var result = CompositeSourceLinkUrlParser.Instance.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeFalse();
        commitSha.Should().BeNull();
        repositoryUrl.Should().BeNull();
    }
}
