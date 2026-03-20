// <copyright file="CharHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Based on https://github.com/dotnet/runtime/blob/v10.0.105/src/libraries/System.Runtime/tests/System.Runtime.Tests/System/CharTests.cs

using System;
using System.Linq;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class CharHelpersTests
{
    private static readonly char[] UppercaseLetters = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'];
    private static readonly char[] LowercaseLetters = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'];
    private static readonly char[] Digits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];
    private static readonly char[] HexDigits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', 'A', 'B', 'C', 'D', 'E', 'F'];

    [Fact]
    public static void IsAsciiLetter()
    {
        for (int i = char.MinValue; i <= char.MaxValue; i++)
        {
            var c = (char)i;

            var expected = UppercaseLetters.Contains(c) || LowercaseLetters.Contains(c);

            CharHelpers.IsAsciiLetter(c).Should().Be(expected);
        }
    }

    [Fact]
    public static void IsAsciiLetterOrDigit()
    {
        for (int i = char.MinValue; i <= char.MaxValue; i++)
        {
            var c = (char)i;

            var expected = UppercaseLetters.Contains(c) || LowercaseLetters.Contains(c) ||  Digits.Contains(c);

            CharHelpers.IsAsciiLetterOrDigit(c).Should().Be(expected);
        }
    }

    [Fact]
    public static void IsAsciiDigit()
    {
        for (int i = char.MinValue; i <= char.MaxValue; i++)
        {
            var c = (char)i;

            var expected = Digits.Contains(c);

            CharHelpers.IsAsciiDigit(c).Should().Be(expected);
        }
    }

    [Fact]
    public static void IsAsciiHexDigit()
    {
        for (int i = char.MinValue; i <= char.MaxValue; i++)
        {
            var c = (char)i;

            var expected = HexDigits.Contains(c);

            CharHelpers.IsAsciiHexDigit(c).Should().Be(expected);
        }
    }

    [Theory]
    [InlineData('a', 'a', 'a', true)]
    [InlineData((char)('a' - 1), 'a', 'a', false)]
    [InlineData((char)('a' + 1), 'a', 'a', false)]
    [InlineData('a', 'a', 'b', true)]
    [InlineData('b', 'a', 'b', true)]
    [InlineData((char)('a' - 1), 'a', 'b', false)]
    [InlineData((char)('b' + 1), 'a', 'b', false)]
    [InlineData('a', 'a', 'z', true)]
    [InlineData('m', 'a', 'z', true)]
    [InlineData('z', 'a', 'z', true)]
    [InlineData((char)('a' - 1), 'a', 'z', false)]
    [InlineData((char)('z' + 1), 'a', 'z', false)]
    [InlineData('\0', '\0', '\uFFFF', true)]
    [InlineData('\u1234', '\0', '\uFFFF', true)]
    [InlineData('\uFFFF', '\0', '\uFFFF', true)]
    [InlineData('\u1234', '\u0123', '\u2345', true)]
    [InlineData('\u1234', '\u2345', '\uFFFF', false)]
    [InlineData('\u1234', '\u0123', '\u1233', false)]
    [InlineData('\u1234', '\u0123', '\u1234', true)]
    [InlineData('\u1234', '\u1235', '\u1231', false)]
    [InlineData('b', 'c', 'd', false)]
    [InlineData('b', 'd', 'c', true)]
    public static void IsBetween_Char(char c, char minInclusive, char maxExclusive, bool expected)
    {
        CharHelpers.IsBetween(c, minInclusive, maxExclusive).Should().Be(expected);
    }
}
