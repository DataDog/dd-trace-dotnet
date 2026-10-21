// <copyright file="GlobalSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using LogEventLevel = Datadog.Trace.Vendors.Serilog.Events.LogEventLevel;

namespace Datadog.Trace.Tests.Configuration
{
    public class GlobalSettingsTests : SettingsTestsBase
    {
        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void DebugEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DebugEnabled, value));
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());

            settings.DebugEnabled.Should().Be(expected);
        }

        [Theory]
        [InlineData("true", "info", true, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("true", "debug", true, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("false", "info", false, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("false", "debug", false, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("A", "info", false, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("A", "debug", false, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData(null, "info", false, null)]
        [InlineData(null, "debug", true, null)]
        [InlineData("", "info", false, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        [InlineData("", "debug", false, (int)Count.OpenTelemetryConfigHiddenByDatadogConfig)]
        public void OtelLogLevelDebugSetsDebugEnabled(string value, string otelValue, bool expected, int? metric)
        {
            const string otelKey = ConfigurationKeys.OpenTelemetry.LogLevel;
            var source = CreateConfigurationSource(
                (ConfigurationKeys.DebugEnabled, value),
                (otelKey, otelValue));
            var errorLog = new OverrideErrorLog();
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.DebugEnabled.Should().Be(expected);
            errorLog.ShouldHaveExpectedOtelMetric(metric, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.DebugEnabled.ToLowerInvariant());
        }

        [Theory]
        [InlineData(null, (int)LogEventLevel.Information)]
        [InlineData("", (int)LogEventLevel.Information)]
        [InlineData("debug", (int)LogEventLevel.Debug)]
        [InlineData("DEBUG", (int)LogEventLevel.Debug)]
        [InlineData("DeBuG", (int)LogEventLevel.Debug)]
        [InlineData("info", (int)LogEventLevel.Information)]
        [InlineData("INFO", (int)LogEventLevel.Information)]
        [InlineData("InFo", (int)LogEventLevel.Information)]
        [InlineData("warn", (int)LogEventLevel.Warning)]
        [InlineData("WARN", (int)LogEventLevel.Warning)]
        [InlineData("WaRn", (int)LogEventLevel.Warning)]
        [InlineData("error", (int)LogEventLevel.Error)]
        [InlineData("ERROR", (int)LogEventLevel.Error)]
        [InlineData("ErRoR", (int)LogEventLevel.Error)]
        public void OtelLogLevelResolvesThreshold(string value, int expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.OpenTelemetry.LogLevel, value));
            var telemetry = new ConfigurationTelemetry();
            var errorLog = new OverrideErrorLog();
            var settings = new GlobalSettings(source, telemetry, errorLog);

            settings.LogLevel.Should().Be((LogEventLevel)expected);
            settings.DebugEnabled.Should().Be(expected == (int)LogEventLevel.Debug);
            if (value is { Length: > 0 })
            {
                telemetry.GetQueueForTesting().Should().OnlyContain(entry => entry.Error == null);
            }

            errorLog.ShouldHaveExpectedOtelMetric((int?)null, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.DebugEnabled.ToLowerInvariant());
        }

        [Theory]
        [InlineData("true", "warn", (int)LogEventLevel.Debug)]
        [InlineData("true", "error", (int)LogEventLevel.Debug)]
        [InlineData("false", "debug", (int)LogEventLevel.Information)]
        [InlineData("false", "warn", (int)LogEventLevel.Information)]
        [InlineData("false", "error", (int)LogEventLevel.Information)]
        [InlineData("1", "error", (int)LogEventLevel.Debug)]
        [InlineData("0", "error", (int)LogEventLevel.Information)]
        [InlineData("A", "error", (int)LogEventLevel.Information)]
        [InlineData("", "error", (int)LogEventLevel.Information)]
        [InlineData("false", "not-a-log-level", (int)LogEventLevel.Information)]
        public void DatadogDebugSettingTakesPrecedence(string debugValue, string otelValue, int expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.DebugEnabled, debugValue),
                (ConfigurationKeys.OpenTelemetry.LogLevel, otelValue));
            var errorLog = new OverrideErrorLog();
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.LogLevel.Should().Be((LogEventLevel)expected);
            settings.DebugEnabled.Should().Be(expected == (int)LogEventLevel.Debug);
            errorLog.ShouldHaveExpectedOtelMetric((int)Count.OpenTelemetryConfigHiddenByDatadogConfig, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.DebugEnabled.ToLowerInvariant());
        }

        [Theory]
        [InlineData("not-a-log-level", false)]
        [InlineData("not-a-log-level", true)]
        [InlineData("trace", false)]
        [InlineData("trace", true)]
        [InlineData("0", false)]
        [InlineData("0", true)]
        public void InvalidOtelLogLevelWarnsAfterInitialization(string value, bool composite)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.OpenTelemetry.LogLevel, value));
            if (composite)
            {
                source = new CompositeConfigurationSource { source };
            }

            var telemetry = new ConfigurationTelemetry();
            var errorLog = new OverrideErrorLog();
            var log = new Mock<IDatadogLogger>();
            var settings = new GlobalSettings(source, telemetry, errorLog);

            settings.LogLevel.Should().Be(LogEventLevel.Information);
            settings.DebugEnabled.Should().BeFalse();
            telemetry.GetQueueForTesting()
                     .Where(entry => entry.Key == ConfigurationKeys.OpenTelemetry.LogLevel)
                     .Should().ContainSingle().Which.Error.Should().NotBeNull();
            log.Invocations.Should().BeEmpty();
            errorLog.Clone().ShouldHaveExpectedOtelMetric((int)Count.OpenTelemetryConfigInvalid, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.DebugEnabled.ToLowerInvariant());

            errorLog.ProcessAndClearActions(log.Object, new MetricsTelemetryCollector());
            errorLog.ProcessAndClearActions(log.Object, new MetricsTelemetryCollector());

            log.Verify(
                logger => logger.Warning(
                    It.IsAny<string>(),
                    ConfigurationKeys.OpenTelemetry.LogLevel,
                    It.IsAny<int>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Theory]
        [InlineData(null, "debug", (int)LogEventLevel.Debug)]
        [InlineData("", "debug", (int)LogEventLevel.Debug)]
        [InlineData("", "warn", (int)LogEventLevel.Warning)]
        [InlineData("", "error", (int)LogEventLevel.Error)]
        public void EmptyOtelLogLevelFallsThroughToLowerPrioritySource(string value, string fallback, int expected)
        {
            var source = new CompositeConfigurationSource
            {
                CreateConfigurationSource((ConfigurationKeys.OpenTelemetry.LogLevel, value)),
                CreateConfigurationSource((ConfigurationKeys.OpenTelemetry.LogLevel, fallback)),
            };
            var errorLog = new OverrideErrorLog();
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.LogLevel.Should().Be((LogEventLevel)expected);
            settings.DebugEnabled.Should().Be(expected == (int)LogEventLevel.Debug);
            errorLog.ShouldHaveExpectedOtelMetric((int?)null, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.DebugEnabled.ToLowerInvariant());
        }

        [Theory]
        [InlineData("debug", (int)LogEventLevel.Debug)]
        [InlineData("info", (int)LogEventLevel.Information)]
        [InlineData("warn", (int)LogEventLevel.Warning)]
        [InlineData("error", (int)LogEventLevel.Error)]
        public void InvalidOtelLogLevelUsesFallbackAndWarns(string fallback, int expected)
        {
            const string rejected = "not-a-log-level";
            var source = new CompositeConfigurationSource
            {
                CreateConfigurationSource((ConfigurationKeys.OpenTelemetry.LogLevel, rejected)),
                CreateConfigurationSource((ConfigurationKeys.OpenTelemetry.LogLevel, fallback)),
            };
            var errorLog = new OverrideErrorLog();
            var log = new Mock<IDatadogLogger>();
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.LogLevel.Should().Be((LogEventLevel)expected);
            errorLog.Clone().ShouldHaveExpectedOtelMetric((int)Count.OpenTelemetryConfigInvalid, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.DebugEnabled.ToLowerInvariant());
            errorLog.ProcessAndClearActions(log.Object, new MetricsTelemetryCollector());
            log.Verify(
                logger => logger.Warning(
                    "OpenTelemetry configuration {OpenTelemetryConfiguration} is invalid.",
                    ConfigurationKeys.OpenTelemetry.LogLevel,
                    It.IsAny<int>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Theory]
        [InlineData("true", "not-a-log-level", (int)LogEventLevel.Debug)]
        [InlineData("false", "not-a-log-level", (int)LogEventLevel.Information)]
        [InlineData("true", "", (int)LogEventLevel.Debug)]
        [InlineData("false", "", (int)LogEventLevel.Information)]
        public void DatadogDebugSettingTakesPrecedenceAcrossSources(string debugValue, string otelValue, int expected)
        {
            var source = new CompositeConfigurationSource
            {
                CreateConfigurationSource((ConfigurationKeys.DebugEnabled, debugValue), (ConfigurationKeys.OpenTelemetry.LogLevel, otelValue)),
                CreateConfigurationSource((ConfigurationKeys.OpenTelemetry.LogLevel, "warn")),
            };
            var errorLog = new OverrideErrorLog();
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.LogLevel.Should().Be((LogEventLevel)expected);
            errorLog.ShouldHaveExpectedOtelMetric((int)Count.OpenTelemetryConfigHiddenByDatadogConfig, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.DebugEnabled.ToLowerInvariant());
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void DiagnosticSourceEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DiagnosticSourceEnabled, value));
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());

            settings.DiagnosticSourceEnabled.Should().Be(expected);
        }
    }
}
