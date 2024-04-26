// <copyright file="RegexBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling;

public class RegexBuilderTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory]
    // regex
    [InlineData(null, "regex", null)]
    [InlineData(".*", "regex", null)]
    [InlineData("", "regex", "^$")]
    [InlineData("test", "regex", "^test$")]
    [InlineData("te.*st", "regex", "^te.*st$")]
    [InlineData("^test", "regex", "^test$")]
    [InlineData("test$", "regex", "^test$")]
    [InlineData("^test$", "regex", "^test$")]
    // glob
    [InlineData(null, "glob", null)]
    [InlineData("*", "glob", null)]
    [InlineData("**", "glob", null)]
    [InlineData("*****", "glob", null)]
    [InlineData("", "glob", "^$")]
    [InlineData("test", "glob", "^test$")]
    [InlineData("te*st", "glob", "^te.*st$")]
    [InlineData("te?st", "glob", "^te.st$")]
    public void Build(string pattern, string format, string expected)
    {
        var regex = RegexBuilder.Build(pattern, format, Timeout);

        if (expected == null)
        {
            regex.Should().BeNull();
        }
        else
        {
            regex.Should().NotBeNull().And.Subject.ToString().Should().Be(expected);
        }
    }
}
