// <copyright file="StringUtilTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class StringUtilTests
{
    public static TheoryData<string?> Values => new()
    {
        null,
        string.Empty,
        "  ",
        "\t",
        "null",
        "not null",
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void StringUtil_IsNullOrEmpty_BehavesAsString(string? input)
    {
        var result = StringUtil.IsNullOrEmpty(input);
        var expected = string.IsNullOrEmpty(input);
        result.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void StringUtil_IsNullOrWhiteSpace_BehavesAsString(string? input)
    {
        var result = StringUtil.IsNullOrWhiteSpace(input);
        var expected = string.IsNullOrWhiteSpace(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void StringUtil_Flow_Analysis_NoErrors()
    {
        string? input = null;
        if (StringUtil.IsNullOrEmpty(input))
        {
            // This is (correctly) flagged as a warning in flow analysis
            // var test = input.Length;
        }
        else
        {
            // This should not flag as a warning in flow analysis
            // if you use string.IsNullOrEmpty() you (incorrectly) get a warning
            var test = input.Length;
        }

        if (StringUtil.IsNullOrWhiteSpace(input))
        {
            // This is (correctly) flagged as a warning in flow analysis
            // var test = input.Length;
        }
        else
        {
            // This should not flag as a warning in flow analysis
            // if you use string.IsNullOrWhiteSpace() you (incorrectly) get a warning
            var test = input.Length;
        }
    }
}
