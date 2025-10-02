// <copyright file="StartupLoggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;
using Datadog.Trace.ClrProfiler.Managed.Loader;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.Managed.Loader;

public class StartupLoggerTests
{
    [Fact]
    public void GetLogDirectoryFromEnvVars_WithExplicitLogDirectory_ReturnsSpecifiedDirectory()
    {
        // we don't modify the result from DD_TRACE_LOG_DIRECTORY, so we can use any path in this test
        const string logsDirectory = "/path/to/logs";

        var envVars = new MockEnvironmentVariableProvider();
        envVars.SetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY", logsDirectory);

        StartupLogger.GetLogDirectoryFromEnvVars(envVars).Should().Be(logsDirectory);
    }

    [Fact]
    public void GetLogDirectoryFromEnvVars_WithLogPath_ReturnsDirectoryFromPath()
    {
        string logsFilename;
        string expectedDirectory;

        // we use Path.GetParent() in this test, which modifies the result from DD_TRACE_LOG_PATH,
        // so we need to adapt the test for Windows or not
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            logsFilename = @"C:\path\to\logs\logs.txt";
            expectedDirectory = @"C:\path\to\logs";
        }
        else
        {
            logsFilename = "/path/to/logs/logs.txt";
            expectedDirectory = "/path/to/logs";
        }

        var envVars = new MockEnvironmentVariableProvider();
        envVars.SetEnvironmentVariable("DD_TRACE_LOG_PATH", logsFilename);

        StartupLogger.GetLogDirectoryFromEnvVars(envVars).Should().Be(expectedDirectory);
    }

    [Fact]
    public void GetLogDirectoryFromEnvVars_WithNoEnvironmentVariables_ReturnsNull()
    {
        var envVars = new MockEnvironmentVariableProvider();
        StartupLogger.GetLogDirectoryFromEnvVars(envVars).Should().BeNull();
    }

    [Fact]
    public void GetDefaultLogDirectory_WithNoEnvironmentVariables_ReturnsDefaultDirectory()
    {
        var envVars = new MockEnvironmentVariableProvider();
        var result = StartupLogger.GetDefaultLogDirectory(envVars);

        result.Should().NotBeNullOrEmpty();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            result.Should().EndWith(@"Datadog .NET Tracer\logs");
        }
        else
        {
            result.Should().Be("/var/log/datadog/dotnet");
        }
    }

    [Fact]
    public void GetDefaultLogDirectory_WithAzureAppServices_ReturnsAzureLogDirectory()
    {
        var envVars = new MockEnvironmentVariableProvider();
        envVars.SetEnvironmentVariable("WEBSITE_SITE_NAME", "my-site-name");

        var result = StartupLogger.GetDefaultLogDirectory(envVars);
        result.Should().NotBeNullOrEmpty();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            result.Should().Be(@"C:\home\LogFiles\datadog");
        }
        else
        {
            result.Should().Be("/home/LogFiles/datadog");
        }
    }
}
