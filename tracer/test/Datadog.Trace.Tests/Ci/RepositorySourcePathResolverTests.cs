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
    [Fact]
    public void ReturnsCurrentDirectoryWhenPathMatchesRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "repository-root");
        var rootWithSeparator = root + Path.DirectorySeparatorChar;

        RepositorySourcePathResolver.MakeRelativePath(root, root, useOSSeparator: true).Should().Be(".");
        RepositorySourcePathResolver.MakeRelativePath(rootWithSeparator, root, useOSSeparator: true).Should().Be(".");
        RepositorySourcePathResolver.MakeRelativePath(root, rootWithSeparator, useOSSeparator: false).Should().Be(".");
    }
}
