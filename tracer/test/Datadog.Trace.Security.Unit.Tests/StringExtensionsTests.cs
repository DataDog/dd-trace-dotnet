// <copyright file="StringExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class StringExtensionsTests
{
    [Theory]
    [InlineData(null, -1)]
    [InlineData("", 0)]
    [InlineData("a", -1964493213)]
    [InlineData("ab", -24234397)]
    [InlineData("abc", 1247340364)]
    [InlineData("𐐷", -1450785469)]
    public void TestEmptyString(string input, int result)
    {
        var i = input.GetStaticHashCode();
        i.Should().Be(result);
    }
}
