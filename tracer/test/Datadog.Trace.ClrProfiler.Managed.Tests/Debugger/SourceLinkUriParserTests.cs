// <copyright file="SourceLinkUriParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Pdb.SourceLink;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class SourceLinkUriParserTests
    {
        public static IEnumerable<object[]> ValidTestCases
        {
            get
            {
                yield return new object[] { "https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/dd35903c688a74b62d1c6a9e4f41371c65704db8/*", "dd35903c688a74b62d1c6a9e4f41371c65704db8", "https://github.com/DataDog/dd-trace-dotnet", typeof(GitHubSourceLinkUrlParser) };
                yield return new object[] { "https://api.bitbucket.org/2.0/repositories/test-org/test-repo/src/dd35903c688a74b62d1c6a9e4f41371c65704db8/*", "dd35903c688a74b62d1c6a9e4f41371c65704db8", "https://bitbucket.org/test-org/test-repo", typeof(BitBucketSourceLinkUrlParser) };
                yield return new object[] { "https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&versionType=commit&version=dd35903c688a74b62d1c6a9e4f41371c65704db8&path=/*", "dd35903c688a74b62d1c6a9e4f41371c65704db8", "https://test.visualstudio.com/test-org/_git/my-repo", typeof(AzureDevOpsSourceLinkUrlParser) };
                yield return new object[] { "https://test-gitlab-domain.com/test-org/test-repo/raw/dd35903c688a74b62d1c6a9e4f41371c65704db8/*", "dd35903c688a74b62d1c6a9e4f41371c65704db8", "https://test-gitlab-domain.com/test-org/test-repo", typeof(GitLabSourceLinkUrlParser) };
                yield return new object[] { "https://dev.azure.com/organisation/project/_apis/git/repositories/example.shopping.api/items?api-version=1.0&versionType=commit&version=0e4d29442102e6cef1c271025d513c8b2187bcd6&path=/*", "0e4d29442102e6cef1c271025d513c8b2187bcd6", "https://dev.azure.com/organisation/project/_git/example.shopping.api", typeof(AzureDevOpsSourceLinkUrlParser) };
            }
        }

        [Theory]
        [MemberData(nameof(ValidTestCases))]
        public void ProviderSpecificParsers_ParsesSuccessfully(string url, string expectedSha, string expectedRepositoryUrl, Type parserType)
        {
            var parser = (SourceLinkUrlParser)Activator.CreateInstance(parserType)!;

            var result = parser.TryParseSourceLinkUrl(new Uri(url), out var sha, out var repositoryUrl);

            result.Should().BeTrue();
            sha.Should().Be(expectedSha);
            repositoryUrl.Should().Be(expectedRepositoryUrl);
        }

        [Theory]
        [MemberData(nameof(ValidTestCases))]
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Testing the general implementation")]
        public void CompositeParser_ParsesSuccessfully(string url, string expectedSha, string expectedRepositoryUrl, Type parserType)
        {
            var result = CompositeSourceLinkUrlParser.Instance.TryParseSourceLinkUrl(new Uri(url), out var sha, out var repositoryUrl);

            result.Should().BeTrue();
            sha.Should().Be(expectedSha);
            repositoryUrl.Should().Be(expectedRepositoryUrl);
        }

        [Theory]
        [InlineData("https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/invalid-sha/*")]
        [InlineData("https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/dd35903c688a74b62d1c6a9e4f41371c65704db8/extraneous/stuff/that/shouldnt/be/here/*")]
        [InlineData("https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/*")]
        [InlineData("https://api.bitbucket.org/2.0/repositories/test-org/test-repo/src/*")] // missing sha
        [InlineData("https://test.visualstudio.com/test-org/_apis/git/repositories/my-repo/items?api-version=1.0&versionType=commit&path=/*")] // missing sha
        [InlineData("https://test-gitlab-domain.com/test-org/test-repo/raw/*")] // missing sha
        [InlineData("https://test.com/test-org/test-repo/raw/invalid-sha/*")] // missing sha
        [InlineData("https://api.bitbucket.org/2.0/repositories/")] // too few segments
        [InlineData("https://test.visualstudio.com/test-org/my-repo/items?api-version=1.0&versionType=commit&path=/*")] // too few segments
        [InlineData("https://test-gitlab-domain.com/test-org/")] // too few segments
        [InlineData("https://test.com/test-org/*")] // too few segments
        public void CompositeParser_ReturnsFalseForInvalidUrl(string url)
        {
            var result = CompositeSourceLinkUrlParser.Instance.TryParseSourceLinkUrl(new Uri(url), out var sha, out var repositoryUrl);

            result.Should().BeFalse();
            sha.Should().BeNull();
            repositoryUrl.Should().BeNull();
        }
    }
}
