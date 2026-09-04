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
    [InlineData("", "e3b0c442-98fc-3c14-9afb-f4c8996fb924")]
    [InlineData("test", "9f86d081-884c-3d65-9a2f-eaa0c55ad015")]
    [InlineData("some very long value that's really quite big", "4a782cd1-d6f9-3135-aeab-347da2475717")]
    [InlineData("12346", "34d128f5-b3de-3e62-ae10-7438fbefabdf")]
    public void ToUuidTests(string input, string expected)
    {
        Datadog.Trace.Debugger.Helpers.StringExtensions.ToUUID(input).Should().Be(expected);
    }
}
