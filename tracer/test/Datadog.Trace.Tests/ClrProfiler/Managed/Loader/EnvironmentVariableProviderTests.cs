// <copyright file="EnvironmentVariableProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.Managed.Loader;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.Managed.Loader;

public class EnvironmentVariableProviderTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("t", true)]
    [InlineData("T", true)]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData("yes", true)]
    [InlineData("Yes", true)]
    [InlineData("YES", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    [InlineData("f", false)]
    [InlineData("F", false)]
    [InlineData("n", false)]
    [InlineData("N", false)]
    [InlineData("no", false)]
    [InlineData("No", false)]
    [InlineData("NO", false)]
    public void GetBooleanEnvironmentVariable_WithValidBooleanValues_ReturnsExpectedBoolean(string value, bool expected)
    {
        var envVars = new MockEnvironmentVariableProvider();
        envVars.SetEnvironmentVariable("TEST_VAR", value);

        envVars.GetBooleanEnvironmentVariable("TEST_VAR").Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("2")]
    [InlineData("maybe")]
    [InlineData("on")]
    [InlineData("off")]
    public void GetBooleanEnvironmentVariable_WithInvalidOrMissingValues_ReturnsNull(string? value)
    {
        var envVars = new MockEnvironmentVariableProvider();
        envVars.SetEnvironmentVariable("TEST_VAR", value);

        envVars.GetBooleanEnvironmentVariable("TEST_VAR").Should().BeNull();
    }
}
