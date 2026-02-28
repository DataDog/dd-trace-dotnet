// <copyright file="DatadogLoggingFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging;

public class DatadogLoggingFactoryTests
{
    public class FileLoggingConfiguration
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("C:/")]
        [InlineData("/var/root")]
        public void UsesLogDirectoryWhenItExists(string obsoleteLogDirectory)
        {
            // will always exist
            var logDirectory = Environment.CurrentDirectory;

            var source = new NameValueConfigurationSource(
                new()
                {
                    { ConfigurationKeys.LogDirectory, logDirectory },
#pragma warning disable CS0618
                    { ConfigurationKeys.TraceLogPath, obsoleteLogDirectory },
#pragma warning restore CS0618
                });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.Should().NotBeNull();
            config.File?.LogDirectory.Should().Be(logDirectory);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void UsesObsoleteLogDirectoryWhenAvailable(string logDirectory)
        {
            // will always exist
            var obsoleteLogDirectory = Environment.CurrentDirectory;
            var obsoleteLogPath = $"{obsoleteLogDirectory}{Path.DirectorySeparatorChar}{Guid.NewGuid()}.log";

            var source = new NameValueConfigurationSource(
                new()
                {
                    { ConfigurationKeys.LogDirectory, logDirectory },
#pragma warning disable CS0618
                    { ConfigurationKeys.TraceLogPath, obsoleteLogPath },
#pragma warning restore CS0618
                });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.Should().NotBeNull();
            config.File?.LogDirectory.Should().Be(obsoleteLogDirectory);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void UsesEnvironmentFallBackWhenBothNull(string logDirectory, string obsoleteLogDirectory)
        {
            var source = new NameValueConfigurationSource(
                new()
                {
                    { ConfigurationKeys.LogDirectory, logDirectory },
#pragma warning disable CS0618
                    { ConfigurationKeys.TraceLogPath, obsoleteLogDirectory },
#pragma warning restore CS0618
                });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.Should().NotBeNull();
            config.File?.LogDirectory.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void CreatesLogDirectoryWhenItDoesntExist()
        {
            var logDirectory = Path.GetTempPath() + Path.DirectorySeparatorChar + Guid.NewGuid();
            Directory.Exists(logDirectory).Should().BeFalse();

            var source = new NameValueConfigurationSource(new() { { ConfigurationKeys.LogDirectory, logDirectory } });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.Should().NotBeNull();
            config.File?.LogDirectory.Should().Be(logDirectory);
            Directory.Exists(logDirectory).Should().BeTrue();
        }
    }

    public class RedactedLogConfiguration
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("true")]
        [InlineData("/var/root")]
        [InlineData("24.54")]
        public void WhenNoOrInvalidConfiguration_TelemetryLogsEnabled(string value)
        {
            var source = new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Telemetry.TelemetryLogsEnabled, value },
            });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.ErrorLogging.Should().NotBeNull();
        }

        [Theory]
        [InlineData("0")]
        [InlineData("false")]
        public void WhenDisabled_TelemetryLogsDisabled(string value)
        {
            var source = new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Telemetry.TelemetryLogsEnabled, value },
            });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.ErrorLogging.Should().BeNull();
        }
    }

    public class SinkConfiguration
    {
        [Fact]
        public void WhenNoSinksProvided_UsesFileSink()
        {
            var source = new NameValueConfigurationSource(new());

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.Should().NotBeNull();
        }

        [Theory]
        [InlineData("file")]
        [InlineData("file,console")]
        [InlineData("console, file")]
        [InlineData("unknown,file")]
        public void WhenFileSinkIsIncluded_UsesFileSink(string sinks)
        {
            var source = new NameValueConfigurationSource(new() { { ConfigurationKeys.LogSinks, sinks } });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.Should().NotBeNull();
        }

        [Theory]
        [InlineData("console")]
        [InlineData("datadog")]
        [InlineData("datadog,console")]
        [InlineData("unknown")]
        public void WhenFileSinkIsNotIncluded_DoesNotUseFileSink(string sinks)
        {
            var source = new NameValueConfigurationSource(new() { { ConfigurationKeys.LogSinks, sinks } });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.Should().BeNull();
        }

        [Theory]
        [InlineData("console-experimental")]
        [InlineData("file,console-experimental")]
        [InlineData("console-experimental, file")]
        [InlineData("unknown,console-experimental")]
        public void WhenConsoleSinkIsIncluded_UsesConsoleSink(string sinks)
        {
            var source = new NameValueConfigurationSource(new() { { ConfigurationKeys.LogSinks, sinks } });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.Console.Should().NotBeNull();
        }

        [Theory]
        [InlineData("file")]
        [InlineData("datadog")]
        [InlineData("datadog,file")]
        [InlineData("unknown")]
        public void WhenConsoleSinkIsNotIncluded_DoesNotUseConsoleSink(string sinks)
        {
            var source = new NameValueConfigurationSource(new() { { ConfigurationKeys.LogSinks, sinks } });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.Console.Should().BeNull();
        }
    }

    [Fact]
#pragma warning disable SA1201 // A method should not follow a class
    public void GetDefaultLogDirectory_WithNoEnvironmentVariables_ReturnsDefaultDirectory()
    {
        var source = new NameValueConfigurationSource(new());
        var result = DatadogLoggingFactory.GetDefaultLogDirectory(source, NullConfigurationTelemetry.Instance);

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
        var source = new NameValueConfigurationSource(
            new()
            {
                { "WEBSITE_SITE_NAME", "my-site-name" }
            });

        var result = DatadogLoggingFactory.GetDefaultLogDirectory(source, NullConfigurationTelemetry.Instance);
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

    [Fact]
    public void GetProgramDataDirectory_OnWindows_ReturnsValidProgramDataPath()
    {
        // Skip on non-Windows platforms
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var result = DatadogLoggingFactory.GetProgramDataDirectory();

        // Should be a rooted path (e.g., C:\ProgramData)
        Path.IsPathRooted(result).Should().BeTrue();

        // Should contain "ProgramData" or "Program Data" (for localized versions)
        // or be the fallback C:\ProgramData
        (result.Contains("ProgramData", StringComparison.OrdinalIgnoreCase) ||
         result.Contains("Program Data", StringComparison.OrdinalIgnoreCase) ||
         result.Equals(@"C:\ProgramData", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();
    }

    [Fact]
    public void TryCreateLogDirectory_WithValidPath_CreatesDirectoryAndReturnsTrue()
    {
        var parentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var logDirectory = Path.Combine(parentDir, "nested", "log", "directory");
        Directory.Exists(logDirectory).Should().BeFalse();
        Directory.Exists(parentDir).Should().BeFalse();

        try
        {
            var result = DatadogLoggingFactory.TryCreateLogDirectory(logDirectory);

            result.Should().BeTrue();
            Directory.Exists(logDirectory).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(parentDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void TryCreateLogDirectory_WithExistingDirectory_ReturnsTrue()
    {
        var logDirectory = Path.GetTempPath();
        Directory.Exists(logDirectory).Should().BeTrue();

        var result = DatadogLoggingFactory.TryCreateLogDirectory(logDirectory);

        result.Should().BeTrue();
    }

    [Fact]
    public void TryCreateLogDirectory_WithInvalidPath_ReturnsFalse()
    {
        // Use an invalid path that cannot be created
        var logDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"Z:\nonexistent\invalid\path\that\cannot\be\created"
            : "/dev/null/nonexistent/invalid/path/that/cannot/be/created";

        var result = DatadogLoggingFactory.TryCreateLogDirectory(logDirectory);

        result.Should().BeFalse();
    }
#pragma warning restore SA1201
}
