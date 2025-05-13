// <copyright file="GlobalSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

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
        [InlineData(null, "info", false, (int)Count.OpenTelemetryConfigInvalid)]
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
        [MemberData(nameof(BooleanTestCases), true)]
        public void DiagnosticSourceEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DiagnosticSourceEnabled, value));
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());

            settings.DiagnosticSourceEnabled.Should().Be(expected);
        }
    }
}
