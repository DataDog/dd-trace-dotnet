// <copyright file="StringUtilTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
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

    [Fact]
    public void ToUpperInvariant_Null_ReturnsNull()
    {
        StringUtil.ToUpperInvariant(null).Should().BeNull();
    }

    [Fact]
    public void ToLowerInvariant_Null_ReturnsNull()
    {
        StringUtil.ToLowerInvariant(null).Should().BeNull();
    }

    // These use the StringUtil polyfills on .NET Framework, and the built-in char overloads on .NET Core
    [Theory]
    [InlineData("", '/')]
    [InlineData("/", '/')]
    [InlineData("/path", '/')]
    [InlineData("path/", '/')]
    [InlineData("path", '/')]
    [InlineData("Path", 'p')]
    public void StartsWithChar_BehavesAsStartsWithString(string value, char c)
    {
        value.StartsWith(c).Should().Be(value.StartsWith(c.ToString(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("", '/')]
    [InlineData("/", '/')]
    [InlineData("/path", '/')]
    [InlineData("path/", '/')]
    [InlineData("path", '/')]
    [InlineData("patH", 'h')]
    public void EndsWithChar_BehavesAsEndsWithString(string value, char c)
    {
        value.EndsWith(c).Should().Be(value.EndsWith(c.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void Invariant_FormatsHolesUsingInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // Build the culture explicitly rather than using e.g. de-DE, because without ICU (globalization-invariant
            // mode, e.g. on Alpine) every predefined culture formats like the invariant culture
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            var minusSign = ((char)0x2212).ToString(); // U+2212 MINUS SIGN, used by e.g. sv-SE under ICU
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = ".";
            culture.NumberFormat.NegativeSign = minusSign;
            culture.DateTimeFormat.TimeSeparator = ".";
            CultureInfo.CurrentCulture = culture;

            var negative = -1;
            var big = 1234567890123L;
            var fraction = 1.5;
            int? missing = null;
            int? present = 42;
            var date = new DateTime(2024, 1, 2, 13, 5, 6, DateTimeKind.Utc);
            string? nullString = null;

            // Make sure the current culture really would format these differently
            string.Format(CultureInfo.CurrentCulture, "{0}|{1}|{2:HH:mm:ss}", negative, fraction, date).Should().Be(minusSign + "1|1,5|13.05.06");

            StringUtil.Invariant($"{negative}|{big}|{fraction}|{missing}|{present}|{date:HH:mm:ss}|{nullString}|{"text"}|{DayOfWeek.Monday}")
                      .Should()
                      .Be("-1|1234567890123|1.5||42|13:05:06||text|Monday");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    [Fact]
    public void Invariant_AppendsSpans()
    {
        var span = "span".AsSpan(1, 2);
        StringUtil.Invariant($"[{span}]").Should().Be("[pa]");
    }
#endif

    [Theory]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData('|')]
    [InlineData(':')]
    [InlineData(' ')]
    [InlineData('\n')]
    [InlineData('#')] // not one of the cached separators
    public void JoinChar_BehavesAsJoinString(char separator)
    {
        var values = new[] { "a", null, string.Empty, "b" };
        var separatorString = separator.ToString();

        string.Join(separator, values).Should().Be(string.Join(separatorString, values));
        string.Join(separator, "a", "b", "c").Should().Be(string.Join(separatorString, "a", "b", "c"));
        string.Join(separator, new List<string?>(values)).Should().Be(string.Join(separatorString, new List<string?>(values)));
        string.Join(separator, new[] { 1, 2, 3 }).Should().Be(string.Join(separatorString, new[] { 1, 2, 3 }));
        string.Join(separator, Array.Empty<string>()).Should().Be(string.Empty);
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

    // These take the code unit as an int, rather than the string directly, so that the unpaired
    // surrogate never reaches the test's display name. vstest serializes display names over its
    // JSON IPC channel, and an unpaired surrogate there aborts the entire test run.
    [Theory]
    [InlineData(0xD83D)] // lone high surrogate
    [InlineData(0xDE00)] // lone low surrogate
    public void ToUpperInvariant_LoneSurrogate_IsSemanticallyEquivalentToBcl(int codeUnit)
    {
        var value = ((char)codeUnit).ToString();

        StringUtil.ToUpperInvariant(value).Should().Be(value.ToUpperInvariant());
    }

    [Theory]
    [InlineData(0xD83D)] // lone high surrogate
    [InlineData(0xDE00)] // lone low surrogate
    public void ToLowerInvariant_LoneSurrogate_IsSemanticallyEquivalentToBcl(int codeUnit)
    {
        var value = ((char)codeUnit).ToString();

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
