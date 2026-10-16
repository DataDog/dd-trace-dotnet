// <copyright file="StartupTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.Managed.Loader;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.Managed.Loader;

public class StartupTests
{
    private const string TracerHome = "/path/to/tracer-home";
    private const string OpenTelemetryAutoHome = "/path/to/otel-auto-home";

    [Theory]
    [InlineData(TracerHome, null, TracerHome)]
    [InlineData(TracerHome, OpenTelemetryAutoHome, TracerHome)]
    [InlineData(null, OpenTelemetryAutoHome, OpenTelemetryAutoHome)]
    [InlineData("", OpenTelemetryAutoHome, OpenTelemetryAutoHome)]
    [InlineData(null, null, null)]
    [InlineData("", "", null)]
    public void GetHomeDirectory_PrefersTracerHomeAndFallsBackToOpenTelemetryAutoHome(string tracerHome, string openTelemetryAutoHome, string expected)
    {
        // we don't modify the resolved directory, so we can use any path in this test
        var envVars = new MockEnvironmentVariableProvider();
        envVars.SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", tracerHome);
        envVars.SetEnvironmentVariable("OTEL_DOTNET_AUTO_HOME", openTelemetryAutoHome);

        Startup.GetHomeDirectory(envVars).Should().Be(expected);
    }
}
