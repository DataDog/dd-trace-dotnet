// <copyright file="AzureDevOpsSourceLinkUrlParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Pdb.SourceLink;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Pdb.SourceLink;

public class AzureDevOpsSourceLinkUrlParserTests
{
    private const string ValidSha = "dd35903c688a74b62d1c6a9e4f41371c65704db8";

    private readonly AzureDevOpsSourceLinkUrlParser _parser = new();

    [Theory]
    [InlineData(
        "https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&versionType=commit&version=" + ValidSha + "&path=/*",
        ValidSha,
        "https://test.visualstudio.com/test-org/_git/my-repo")]
    [InlineData(
        "https://dev.azure.com/organisation/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=" + ValidSha + "&path=/*",
        ValidSha,
        "https://dev.azure.com/organisation/project/_git/repo")]
    [InlineData(
        "https://dev.azure.com/org/proj/_apis/git/repositories/example.shopping.api/items?api-version=1.0&versionType=commit&version=0e4d29442102e6cef1c271025d513c8b2187bcd6&path=/*",
        "0e4d29442102e6cef1c271025d513c8b2187bcd6",
        "https://dev.azure.com/org/proj/_git/example.shopping.api")]
    public void TryParseSourceLinkUrl_ValidUrl_ReturnsTrue(string url, string expectedSha, string expectedRepoUrl)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeTrue();
        commitSha.Should().Be(expectedSha);
        repositoryUrl.Should().Be(expectedRepoUrl);
    }

    [Theory]
    [InlineData("https://test.visualstudio.com/test-org/items?api-version=1.0&versionType=commit&version=" + ValidSha + "&path=/*")] // missing _apis/git/repositories/
    [InlineData("https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&version=" + ValidSha + "&path=/*")] // missing versionType=commit
    [InlineData("https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&versionType=commit&path=/*")] // missing version=
    [InlineData("https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&versionType=commit&version=" + ValidSha)] // missing path=/*
    [InlineData("https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&versionType=commit&version=invalid-sha&path=/*")] // invalid sha
    [InlineData("https://github.com/org/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=" + ValidSha + "&path=/*")] // unsupported host
    [InlineData("https://test.visualstudio.com/_apis/git/repositories/items?api-version=1.0&versionType=commit&version=" + ValidSha + "&path=/*")] // too few path segments (< 5)
    [InlineData("https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&versionType=commit&version=&path=/*")] // empty version value
    [InlineData("https://dev.azure.com/org/proj/_apis/git/repositories/?versionType=commit&version=" + ValidSha + "&path=/*")] // missing repo name (trailing slash, empty segment)
    public void TryParseSourceLinkUrl_InvalidUrl_ReturnsFalse(string url)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out _, out var repositoryUrl);

        result.Should().BeFalse();
        repositoryUrl.Should().BeNull();
    }
}
