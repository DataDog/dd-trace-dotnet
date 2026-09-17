// <copyright file="RepositorySourcePathResolverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.IO;
using Datadog.Trace.Ci.CodeOwnership;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class RepositorySourcePathResolverTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReturnsCurrentDirectoryWhenPathsAreIdentical(bool useOSSeparator)
    {
        var root = Path.Combine(Path.GetTempPath(), "repository-root");

        RepositorySourcePathResolver.MakeRelativePath(root, root, useOSSeparator).Should().Be(".");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReturnsCurrentDirectoryWhenPathsOnlyDifferByTrailingSeparator(bool useOSSeparator)
    {
        var root = Path.Combine(Path.GetTempPath(), "repository-root");
        var rootWithSeparator = root + Path.DirectorySeparatorChar;

        RepositorySourcePathResolver.MakeRelativePath(rootWithSeparator, root, useOSSeparator).Should().Be(".");
        RepositorySourcePathResolver.MakeRelativePath(root, rootWithSeparator, useOSSeparator).Should().Be(".");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PathEqualityUsesCurrentPlatformCaseSensitivity(bool useOSSeparator)
    {
        var root = Path.Combine(Path.GetTempPath(), "repository-root");
        var alternateCasePath = Path.Combine(Path.GetTempPath(), "Repository-Root");

        var relativePath = RepositorySourcePathResolver.MakeRelativePath(root, alternateCasePath, useOSSeparator);

        if (FrameworkDescription.Instance.IsWindows())
        {
            relativePath.Should().Be(".");
        }
        else
        {
            relativePath.Should().NotBe(".");
        }
    }
}
