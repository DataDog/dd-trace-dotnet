// <copyright file="BitBucketServerSourceLinkUrlParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Pdb.SourceLink;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Pdb.SourceLink;

public class BitBucketServerSourceLinkUrlParserTests
{
    private const string ValidSha = "dd35903c688a74b62d1c6a9e4f41371c65704db8";

    private readonly BitBucketServerSourceLinkUrlParser _parser = new();

    [Theory]
    // Bitbucket Server >= 4.7: /projects/{project}/repos/{repo}/raw/*?at={sha}
    [InlineData(
        "https://bitbucket.mycompany.com/projects/MYPROJ/repos/myrepo/raw/*?at=" + ValidSha,
        ValidSha,
        "https://bitbucket.mycompany.com/projects/MYPROJ/repos/myrepo")]
    // With base path
    [InlineData(
        "https://stash.example.com/base/projects/PROJ/repos/my-repo/raw/*?at=" + ValidSha,
        ValidSha,
        "https://stash.example.com/base/projects/PROJ/repos/my-repo")]
    // Bitbucket Server < 4.7: /projects/{project}/repos/{repo}/browse/*?at={sha}&raw
    [InlineData(
        "http://stash.mycompany.com:7990/projects/cclcom/repos/myrepo/browse/*?at=" + ValidSha + "&raw",
        ValidSha,
        "http://stash.mycompany.com:7990/projects/cclcom/repos/myrepo")]
    // < 4.7 with base path
    [InlineData(
        "https://bitbucket.internal/base/projects/TEAM/repos/core-lib/browse/*?at=" + ValidSha + "&raw",
        ValidSha,
        "https://bitbucket.internal/base/projects/TEAM/repos/core-lib")]
    // < 4.7 with raw flag before at= (order should not matter)
    [InlineData(
        "https://stash.mycompany.com/projects/PROJ/repos/repo/browse/*?raw&at=" + ValidSha,
        ValidSha,
        "https://stash.mycompany.com/projects/PROJ/repos/repo")]
    // >= 4.7 with additional query params before at=
    [InlineData(
        "https://bitbucket.mycompany.com/projects/MYPROJ/repos/myrepo/raw/*?limit=100&at=" + ValidSha,
        ValidSha,
        "https://bitbucket.mycompany.com/projects/MYPROJ/repos/myrepo")]
    public void TryParseSourceLinkUrl_ValidUrl_ReturnsTrue(string url, string expectedSha, string expectedRepoUrl)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeTrue();
        commitSha.Should().Be(expectedSha);
        repositoryUrl.Should().Be(expectedRepoUrl);
    }

    [Theory]
    [InlineData("https://bitbucket.mycompany.com/projects/MYPROJ/repos/myrepo/raw/*")] // missing at= query parameter
    [InlineData("https://bitbucket.mycompany.com/repos/myrepo/raw/*?at=" + ValidSha)] // missing /projects/
    [InlineData("https://bitbucket.mycompany.com/projects/MYPROJ/myrepo/raw/*?at=" + ValidSha)] // missing /repos/
    [InlineData("https://bitbucket.mycompany.com/projects/MYPROJ/repos/myrepo/raw/*?at=invalid-sha")] // invalid sha
    [InlineData("https://bitbucket.mycompany.com/projects/MYPROJ/repos/myrepo/blob/*?at=" + ValidSha)] // /blob/ instead of /raw/ or /browse/
    [InlineData("https://bitbucket.mycompany.com/projects/MYPROJ/repos/myrepo/browse/*?at=" + ValidSha)] // /browse/ without &raw query flag
    [InlineData("https://bitbucket.mycompany.com/projects/MYPROJ/repos/myrepo/browse/*?at=" + ValidSha + "&raws=1")] // "raws" is not the standalone "raw" flag
    public void TryParseSourceLinkUrl_InvalidUrl_ReturnsFalse(string url)
    {
        var result = _parser.TryParseSourceLinkUrl(new Uri(url), out var commitSha, out var repositoryUrl);

        result.Should().BeFalse();
        commitSha.Should().BeNull();
        repositoryUrl.Should().BeNull();
    }
}
