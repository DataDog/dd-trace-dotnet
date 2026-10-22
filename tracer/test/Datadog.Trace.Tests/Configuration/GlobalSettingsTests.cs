// <copyright file="GlobalSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
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
        [InlineData("true", "warn", "error", (int)LogEventLevel.Debug)]
        [InlineData("false", "warn", "error", (int)LogEventLevel.Warning)]
        [InlineData(null, "warn", "error", (int)LogEventLevel.Warning)]
        [InlineData("false", null, "debug", (int)LogEventLevel.Debug)]
        [InlineData("false", null, "warn", (int)LogEventLevel.Warning)]
        [InlineData("false", null, "error", (int)LogEventLevel.Error)]
        [InlineData("false", null, null, (int)LogEventLevel.Information)]
        [InlineData(null, "DeBuG", null, (int)LogEventLevel.Debug)]
        [InlineData(null, "InFo", null, (int)LogEventLevel.Information)]
        [InlineData(null, "WaRn", null, (int)LogEventLevel.Warning)]
        [InlineData(null, "ErRoR", null, (int)LogEventLevel.Error)]
        [InlineData(null, "", "warn", (int)LogEventLevel.Warning)]
        [InlineData(null, "invalid", null, (int)LogEventLevel.Information)]
        [InlineData(null, "invalid", "warn", (int)LogEventLevel.Warning)]
        [InlineData("false", "invalid", "error", (int)LogEventLevel.Error)]
        [InlineData("true", "invalid", "warn", (int)LogEventLevel.Debug)]
        public void ManagedLogLevelPrecedence(string debug, string datadog, string otel, int expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.DebugEnabled, debug),
                (ConfigurationKeys.Agent.LogLevel, datadog),
                (ConfigurationKeys.OpenTelemetry.LogLevel, otel));
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());

            settings.LogLevel.Should().Be((LogEventLevel)expected);
            settings.DebugEnabled.Should().Be(expected == (int)LogEventLevel.Debug);
        }

        [Theory]
        [InlineData("true", "warn", "DD_TRACE_DEBUG")]
        [InlineData("false", "warn", "DD_LOG_LEVEL")]
        [InlineData(null, "warn", "DD_LOG_LEVEL")]
        public void LogLevelOverrideReportsWinningDatadogKey(string debug, string datadog, string expectedKey)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.DebugEnabled, debug),
                (ConfigurationKeys.Agent.LogLevel, datadog),
                (ConfigurationKeys.OpenTelemetry.LogLevel, "error"));
            var errorLog = new OverrideErrorLog();
            _ = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            errorLog.ShouldHaveExpectedOtelMetric(
                (int)Count.OpenTelemetryConfigHiddenByDatadogConfig,
                ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(),
                expectedKey.ToLowerInvariant());
        }

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
        [InlineData("false", "info", false, null)]
        [InlineData("false", "debug", true, null)]
        [InlineData("A", "info", false, null)]
        [InlineData("A", "debug", true, null)]
        [InlineData(null, "info", false, null)]
        [InlineData(null, "debug", true, null)]
        [InlineData("", "info", false, null)]
        [InlineData("", "debug", true, null)]
        public void OtelLogLevelDebugSetsDebugEnabled(string value, string otelValue, bool expected, int? metric)
        {
            const string otelKey = ConfigurationKeys.OpenTelemetry.LogLevel;
            var source = CreateConfigurationSource(
                (ConfigurationKeys.DebugEnabled, value),
                (otelKey, otelValue));
            var errorLog = new OverrideErrorLog();
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.DebugEnabled.Should().Be(expected);
            errorLog.ShouldHaveExpectedOtelMetric(
                metric,
                ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(),
                (metric == (int)Count.OpenTelemetryConfigHiddenByDatadogConfig ? ConfigurationKeys.DebugEnabled : ConfigurationKeys.Agent.LogLevel).ToLowerInvariant());
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

            errorLog.ShouldHaveExpectedOtelMetric((int?)null, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.Agent.LogLevel.ToLowerInvariant());
        }

        [Theory]
        [InlineData("true", "warn", (int)LogEventLevel.Debug)]
        [InlineData("true", "error", (int)LogEventLevel.Debug)]
        [InlineData("false", "debug", (int)LogEventLevel.Debug)]
        [InlineData("false", "warn", (int)LogEventLevel.Warning)]
        [InlineData("false", "error", (int)LogEventLevel.Error)]
        [InlineData("1", "error", (int)LogEventLevel.Debug)]
        [InlineData("0", "error", (int)LogEventLevel.Error)]
        [InlineData("A", "error", (int)LogEventLevel.Error)]
        [InlineData("", "error", (int)LogEventLevel.Error)]
        [InlineData("false", "not-a-log-level", (int)LogEventLevel.Information)]
        public void DebugAndOtelLevelResolvePrecedence(string debugValue, string otelValue, int expected)
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.DebugEnabled, debugValue),
                (ConfigurationKeys.OpenTelemetry.LogLevel, otelValue));
            var errorLog = new OverrideErrorLog();
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.LogLevel.Should().Be((LogEventLevel)expected);
            settings.DebugEnabled.Should().Be(expected == (int)LogEventLevel.Debug);
            int? metric = debugValue is "true" or "1"
                              ? (int)Count.OpenTelemetryConfigHiddenByDatadogConfig
                              : otelValue == "not-a-log-level" ? (int)Count.OpenTelemetryConfigInvalid : null;
            errorLog.ShouldHaveExpectedOtelMetric(
                metric,
                ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(),
                (metric == (int)Count.OpenTelemetryConfigHiddenByDatadogConfig ? ConfigurationKeys.DebugEnabled : ConfigurationKeys.Agent.LogLevel).ToLowerInvariant());
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
            errorLog.Clone().ShouldHaveExpectedOtelMetric((int)Count.OpenTelemetryConfigInvalid, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.Agent.LogLevel.ToLowerInvariant());

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
            errorLog.ShouldHaveExpectedOtelMetric((int?)null, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.Agent.LogLevel.ToLowerInvariant());
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
            errorLog.Clone().ShouldHaveExpectedOtelMetric((int)Count.OpenTelemetryConfigInvalid, ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(), ConfigurationKeys.Agent.LogLevel.ToLowerInvariant());
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
        [InlineData("false", "not-a-log-level", (int)LogEventLevel.Warning)]
        [InlineData("true", "", (int)LogEventLevel.Debug)]
        [InlineData("false", "", (int)LogEventLevel.Warning)]
        public void DebugAndOtelLevelResolvePrecedenceAcrossSources(string debugValue, string otelValue, int expected)
        {
            var source = new CompositeConfigurationSource
            {
                CreateConfigurationSource((ConfigurationKeys.DebugEnabled, debugValue), (ConfigurationKeys.OpenTelemetry.LogLevel, otelValue)),
                CreateConfigurationSource((ConfigurationKeys.OpenTelemetry.LogLevel, "warn")),
            };
            var errorLog = new OverrideErrorLog();
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.LogLevel.Should().Be((LogEventLevel)expected);
            int? metric = debugValue is "true" or "1"
                              ? (int)Count.OpenTelemetryConfigHiddenByDatadogConfig
                              : otelValue == "not-a-log-level" ? (int)Count.OpenTelemetryConfigInvalid : null;
            errorLog.ShouldHaveExpectedOtelMetric(
                metric,
                ConfigurationKeys.OpenTelemetry.LogLevel.ToLowerInvariant(),
                (metric == (int)Count.OpenTelemetryConfigHiddenByDatadogConfig ? ConfigurationKeys.DebugEnabled : ConfigurationKeys.Agent.LogLevel).ToLowerInvariant());
        }

        [Theory]
        [InlineData(null, "warn", "warn", ConfigurationOrigins.Code)]
        [InlineData(null, null, "info", ConfigurationOrigins.Default)]
        [InlineData("invalid", "warn", "warn", ConfigurationOrigins.Code)]
        public void DatadogLogLevelTelemetryAndSourceFallback(string rejected, string value, string expected, ConfigurationOrigins origin)
        {
            var source = new CompositeConfigurationSource
            {
                CreateConfigurationSource((ConfigurationKeys.Agent.LogLevel, rejected)),
                CreateConfigurationSource((ConfigurationKeys.Agent.LogLevel, value)),
            };
            var telemetry = new ConfigurationTelemetry();
            var settings = new GlobalSettings(source, telemetry, new OverrideErrorLog());

            settings.LogLevel.Should().Be(expected == "warn" ? LogEventLevel.Warning : LogEventLevel.Information);
            var entries = telemetry.GetQueueForTesting().Where(entry => entry.Key == ConfigurationKeys.Agent.LogLevel).ToArray();
            entries.Should().Contain(entry => entry.StringValue == expected && entry.Origin == origin && entry.Error == null);
            if (rejected is not null)
            {
                entries.Should().Contain(entry => entry.Error != null);
            }
        }

        [Theory]
        [InlineData("json", "invalid", true)]
        [InlineData("json", "", false)]
        [InlineData("object", "invalid", true)]
        [InlineData("object", "", false)]
        [InlineData("string", "invalid", true)]
        [InlineData("string", "", false)]
        public void OtelFallbackRetainsDiagnosticsAcrossSourceTypes(string kind, string rejected, bool warns)
        {
            const string key = ConfigurationKeys.OpenTelemetry.LogLevel;
            IConfigurationSource first = kind switch
            {
                "json" => new JsonConfigurationSource("{\"OTEL_LOG_LEVEL\":\"" + rejected + "\"}", ConfigurationOrigins.Code),
                "object" => new DictionaryObjectConfigurationSource(new Dictionary<string, object> { [key] = rejected }),
                _ => new NameValueConfigurationSource(new NameValueCollection { { key, rejected } }),
            };
            var source = new CompositeConfigurationSource
            {
                first,
                CreateConfigurationSource((key, "warn")),
            };
            var errorLog = new OverrideErrorLog();
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, errorLog);

            settings.LogLevel.Should().Be(LogEventLevel.Warning);
            errorLog.ShouldHaveExpectedOtelMetric(
                warns ? (int)Count.OpenTelemetryConfigInvalid : (int?)null,
                key.ToLowerInvariant(),
                ConfigurationKeys.Agent.LogLevel.ToLowerInvariant());
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
