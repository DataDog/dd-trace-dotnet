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

#if NETFRAMEWORK
    [Theory]
    [MemberData(nameof(Data.SemanticEquivalenceInputs), MemberType = typeof(Data))]
    public void ToUpperInvariant_IsSemanticallyEquivalentToBcl(string value)
    {
        StringUtil.ToUpperInvariant(value).Should().Be(value.ToUpperInvariant());
    }

    [Theory]
    [MemberData(nameof(Data.SemanticEquivalenceInputs), MemberType = typeof(Data))]
    public void ToLowerInvariant_IsSemanticallyEquivalentToBcl(string value)
    {
        StringUtil.ToLowerInvariant(value).Should().Be(value.ToLowerInvariant());
    }

    [Theory]
    [MemberData(nameof(Data.AsciiNoOpInputs), MemberType = typeof(Data))]
    public void ToUpperInvariant_AlreadyUppercaseAscii_ReturnsSameInstance(string value)
    {
        // Build an entirely fresh instance to make sure
        var input = new string(value.ToUpperInvariant().ToCharArray());

        var result = StringUtil.ToUpperInvariant(input);

        result.Should().BeSameAs(input);
    }

    [Theory]
    [MemberData(nameof(Data.AsciiNoOpInputs), MemberType = typeof(Data))]
    public void ToLowerInvariant_AlreadyLowercaseAscii_ReturnsSameInstance(string value)
    {
        var input = new string(value.ToLowerInvariant().ToCharArray());

        var result = StringUtil.ToLowerInvariant(input);

        result.Should().BeSameAs(input);
    }

    [Fact]
    public void ToUpperInvariant_ContainingAsciiLowercase_ReturnsNewInstance()
    {
        var input = new string("already Upper".ToCharArray());

        var result = StringUtil.ToUpperInvariant(input);

        result.Should().NotBeSameAs(input);
        result.Should().Be(input.ToUpperInvariant());
    }

    [Fact]
    public void ToLowerInvariant_ContainingAsciiUppercase_ReturnsNewInstance()
    {
        var input = new string("already Lower".ToCharArray());

        var result = StringUtil.ToLowerInvariant(input);

        result.Should().NotBeSameAs(input);
        result.Should().Be(input.ToLowerInvariant());
    }

    public static class Data
    {
        public static readonly object[][] SemanticEquivalenceInputs =
        {
            [string.Empty],
            ["a"],
            ["A"],
            ["already upper".ToUpperInvariant()],
            ["already lower".ToLowerInvariant()],
            ["MixedCase123!@#"],
            ["İstanbul"], // U+0130 LATIN CAPITAL LETTER I WITH DOT ABOVE -> lowercases across the ASCII boundary
            ["K"], // U+212A KELVIN SIGN -> lowercases to 'k'
            ["ſ"], // U+017F LATIN SMALL LETTER LONG S -> uppercases to 'S'
            ["École"],
            ["ÉCOLE"],
            ["grüße"],
            ["GRÜSSE"],
            ["😀"], // surrogate pair (U+1F600), aligned at index 0
            ["a😀"], // surrogate pair unaligned to the 2-char stride
            ["ab😀"], // surrogate pair aligned again after an even prefix
            ["\uD83D"], // lone high surrogate
            ["\uDE00"], // lone low surrogate
            ["\u0000"], // NUL - lowest code point
            ["\u007F"], // DEL - last ASCII code point
            ["\u0080"], // first non-ASCII code point (C1 control range)
            ["az"],
            [new string('x', 200)], // long all-ASCII no-op input
            [new string('X', 200)],
            ["/API/v2/Orders/{id}/Items"],
            ["get"],
            ["GET"],
        };

        public static readonly object[][] AsciiNoOpInputs =
        {
            [string.Empty],
            ["a"],
            ["z"],
            ["0123456789"],
            ["already upper text with spaces and !@#$%^&*()"],
            [new string('x', 199)], // odd length
            [new string('x', 200)], // even length
        };
    }
#endif
}
