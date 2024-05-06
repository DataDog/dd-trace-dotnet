// <copyright file="CIVisibilitySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class CIVisibilitySettingsTests : SettingsTestsBase
    {
        [Theory]
        [MemberData(nameof(BooleanTestCases), null)]
        public void Enabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.Enabled, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.Enabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Agentless(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.AgentlessEnabled, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.Agentless.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Logs(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.Logs, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.Logs.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ApiKey(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApiKey, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.ApiKey.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "datadoghq.com", Strings.AllowEmpty)]
        public void Site(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Site, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.Site.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void AgentlessUrl(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.AgentlessUrl, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.AgentlessUrl.Should().Be(expected);
        }

        [Fact]
        public void MaximumAgentlessPayloadSize()
        {
            var source = CreateConfigurationSource();
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.MaximumAgentlessPayloadSize.Should().Be(5 * 1024 * 1024);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ProxyHttps(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Proxy.ProxyHttps, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.ProxyHttps.Should().Be(expected);
        }

        [Theory]
        [InlineData("", new string[0])]
        [InlineData(null, new string[0])]
        [InlineData("value", new[] { "value" })]
        [InlineData("value1 value2", new[] { "value1", "value2" })]
        public void ProxyNoProxy(string value, string[] expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Proxy.ProxyNoProxy, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.ProxyNoProxy.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void IntelligentTestRunnerEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.IntelligentTestRunnerEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), parameters: new object[] { null })]
        public void TestsSkippingEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.TestsSkippingEnabled, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.TestsSkippingEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), parameters: new object[] { null })]
        public void CodeCoverageEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverage, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.CodeCoverageEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CodeCoverageSnkFilePath(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverageSnkFile, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.CodeCoverageSnkFilePath.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CodeCoveragePath(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoveragePath, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.CodeCoveragePath.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void CodeCoverageEnableJitOptimizations(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverageEnableJitOptimizations, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.CodeCoverageEnableJitOptimizations.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), parameters: new object[] { null })]
        public void GitUploadEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.GitUploadEnabled, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.GitUploadEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ForceAgentsEvpProxy(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy, value));
            var settings = new CIVisibilitySettings(source, NullConfigurationTelemetry.Instance);

            settings.ForceAgentsEvpProxy.Should().Be(expected);
        }
    }
}
