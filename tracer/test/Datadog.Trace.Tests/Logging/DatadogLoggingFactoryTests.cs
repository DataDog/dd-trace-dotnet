// <copyright file="DatadogLoggingFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.Internal.Configuration;
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
                    { ConfigurationKeys.ProfilerLogPath, obsoleteLogDirectory },
#pragma warning restore CS0618
                });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.HasValue.Should().BeTrue();
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
                    { ConfigurationKeys.ProfilerLogPath, obsoleteLogPath },
#pragma warning restore CS0618
                });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.HasValue.Should().BeTrue();
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
                    { ConfigurationKeys.ProfilerLogPath, obsoleteLogDirectory },
#pragma warning restore CS0618
                });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.HasValue.Should().BeTrue();
            config.File?.LogDirectory.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void CreatesLogDirectoryWhenItDoesntExist()
        {
            var logDirectory = Path.GetTempPath() + Path.DirectorySeparatorChar + Guid.NewGuid();
            Directory.Exists(logDirectory).Should().BeFalse();

            var source = new NameValueConfigurationSource(new() { { ConfigurationKeys.LogDirectory, logDirectory } });

            var config = DatadogLoggingFactory.GetConfiguration(source, NullConfigurationTelemetry.Instance);
            config.File.HasValue.Should().BeTrue();
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
            config.File.HasValue.Should().BeTrue();
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
            config.File.HasValue.Should().BeTrue();
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
            config.File.HasValue.Should().BeFalse();
        }
    }
}
