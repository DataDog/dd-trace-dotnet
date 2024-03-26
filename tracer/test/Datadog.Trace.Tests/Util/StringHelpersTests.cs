// <copyright file="StringHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class StringHelpersTests
{
    private static readonly string String100 = string.Join(string.Empty, Enumerable.Repeat("0123456789", 10));
    private static readonly string String200 = string.Join(string.Empty, Enumerable.Repeat("9876543210", 20));
    private static readonly string String300 = string.Join(string.Empty, Enumerable.Repeat("abcdefghij", 30));

    public static string[] GetCases(int arg)
        => [null, string.Empty, $"String{arg}", $"{String100}{arg}", $"{String200}{arg}", $"{String300}{arg}"];

    public static IEnumerable<object[]> GetTwoArgs()
        => from str0 in GetCases(0)
           from str1 in GetCases(1)
           select new object[] { str0, str1 };

    public static IEnumerable<object[]> GetThreeArgs()
        => from str0 in GetCases(0)
           from str1 in GetCases(1)
           from str2 in GetCases(2)
           select new object[] { str0, str1, str2 };

    [Theory]
    [MemberData(nameof(GetTwoArgs))]
    public void Concat_ReturnsExpected_TwoArg(string str0, string str1)
    {
        var expected = string.Concat(str0, str1);
        // Make sure we use the "real" span, not the vendored version
        var actual = StringHelpers.Concat(System.MemoryExtensions.AsSpan(str0), System.MemoryExtensions.AsSpan(str1));

        actual.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(GetThreeArgs))]
    public void Concat_ReturnsExpected_ThreeArg(string str0, string str1, string str2)
    {
        var expected = string.Concat(str0, str1, str2);
        // Make sure we use the "real" span, not the vendored version
        var actual = StringHelpers.Concat(System.MemoryExtensions.AsSpan(str0), System.MemoryExtensions.AsSpan(str1), System.MemoryExtensions.AsSpan(str2));

        actual.Should().Be(expected);
    }
}
#endif
