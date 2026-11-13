// <copyright file="CIEnvironmentIsolationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Area", "CIVisibility")]
[Trait("RunOnWindows", "True")]
public class CIEnvironmentIsolationTests(ITestOutputHelper output) : TestHelper("XUnitTests", output)
{
    [Theory]
    [InlineData("X_DATADOG_TRACE_ID")]
    [InlineData("X_DATADOG_PARENT_ID")]
    [InlineData("X_DATADOG_SAMPLING_PRIORITY")]
    [InlineData("X_DATADOG_ORIGIN")]
    [InlineData("X_DATADOG_TAGS")]
    [InlineData("TRACEPARENT")]
    [InlineData("TRACESTATE")]
    [InlineData("BAGGAGE")]
    [InlineData("B3")]
    [InlineData("X_B3_TRACEID")]
    [InlineData("X_B3_SPANID")]
    [InlineData("X_B3_SAMPLED")]
    [InlineData("X_B3_FLAGS")]
    [InlineData(ConfigurationKeys.CIVisibility.TestSessionCommand)]
    [InlineData(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory)]
    public void ClearsInheritedSessionWithoutChangingParentEnvironment(string key)
    {
        var parentEnvironment = new Dictionary<string, string>
        {
            [key] = "inherited-session",
            ["HOME"] = "home-directory",
            ["USERPROFILE"] = "user-profile",
        };
        var childEnvironment = new Dictionary<string, string>(parentEnvironment);

        ClearCIEnvironmentVariables();
        foreach (var variable in EnvironmentHelper.CustomEnvironmentVariables)
        {
            childEnvironment[variable.Key] = variable.Value;
        }

        childEnvironment.Should().Contain(key, string.Empty);
        childEnvironment.Should().Contain("HOME", "home-directory");
        childEnvironment.Should().Contain("USERPROFILE", "user-profile");
        parentEnvironment.Should().Contain(key, "inherited-session");

        // Tests that intentionally inject a session can still override the clean defaults.
        SetEnvironmentVariable(key, "explicit-session");
        EnvironmentHelper.CustomEnvironmentVariables.Should().Contain(key, "explicit-session");
    }
}
