// <copyright file="UtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class UtilsTests
{
    public static IEnumerable<object?[]> ParseWhereIsOutputData()
    {
        yield return ["dotnet: /usr/bin/dotnet /usr/lib/dotnet /etc/dotnet", new[] { "/usr/bin/dotnet", "/usr/lib/dotnet", "/etc/dotnet" }];
        yield return ["git: /usr/bin/git", new[] { "/usr/bin/git" }];
        yield return ["cmake: /usr/bin/cmake /usr/lib/cmake /usr/share/cmake", new[] { "/usr/bin/cmake", "/usr/lib/cmake", "/usr/share/cmake" }];
        yield return ["docker:", Array.Empty<string>()];
        yield return [string.Empty, Array.Empty<string>()];
        yield return [null, Array.Empty<string>()];
    }

    [Theory]
    [MemberData(nameof(ParseWhereIsOutputData))]
    public void ParseWhereisOutputTests(string line, string[] expectedPaths)
    {
        var actualPath = ProcessHelpers.ParseWhereisOutput(line);
        actualPath.Should().BeEquivalentTo(expectedPaths);
    }
}
