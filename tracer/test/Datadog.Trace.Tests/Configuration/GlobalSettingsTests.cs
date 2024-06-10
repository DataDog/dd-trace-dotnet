// <copyright file="GlobalSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
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
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance);

            settings.DebugEnabled.Should().Be(expected);
        }

        [Theory]
        [InlineData("true", "info", true)]
        [InlineData("true", "debug", true)]
        [InlineData("false", "info", false)]
        [InlineData("false", "debug", false)]
        [InlineData("A", "info", false)]
        [InlineData("A", "debug", false)]
        [InlineData(null, "info", false)]
        [InlineData(null, "debug", true)]
        [InlineData("", "info", false)]
        [InlineData("", "debug", false)]
        public void OtelLogLevelDebugSetsDebugEnabled(string value, string otelValue, bool expected)
        {
            const string otelKey = ConfigurationKeys.OpenTelemetry.LogLevel;
            var source = CreateConfigurationSource(
                (ConfigurationKeys.DebugEnabled, value),
                (otelKey, otelValue));
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance);

            settings.DebugEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void DiagnosticSourceEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.DiagnosticSourceEnabled, value));
            var settings = new GlobalSettings(source, NullConfigurationTelemetry.Instance);

            settings.DiagnosticSourceEnabled.Should().Be(expected);
        }
    }
}
