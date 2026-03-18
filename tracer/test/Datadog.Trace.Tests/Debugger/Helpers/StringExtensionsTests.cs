// <copyright file="StringExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.Helpers;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("", "d41d8cd9-8f00-3204-a980-0998ecf8427e")]
    [InlineData("test", "098f6bcd-4621-3373-8ade-4e832627b4f6")]
    [InlineData("some very long value that's really quite big", "d1ceb9c4-c3ef-3626-827e-c82a9895930f")]
    [InlineData("12346", "a3590023-df66-3c92-ae35-e3316026d17d")]
    public void ToUuidTests(string input, string expected)
    {
        Datadog.Trace.Debugger.Helpers.StringExtensions.ToUUID(input).Should().Be(expected);
    }
}
