// <copyright file="CIVisibilitySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class CIVisibilitySettingsTests : SettingsTestsBase
    {
        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Enabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.Enabled, value));
            var settings = new CIVisibilitySettings(source);

            settings.Enabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Agentless(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.AgentlessEnabled, value));
            var settings = new CIVisibilitySettings(source);

            settings.Agentless.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Logs(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.Logs, value));
            var settings = new CIVisibilitySettings(source);

            settings.Logs.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ApiKey(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApiKey, value));
            var settings = new CIVisibilitySettings(source);

            settings.ApiKey.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ApplicationKey(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApplicationKey, value));
            var settings = new CIVisibilitySettings(source);

            settings.ApplicationKey.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "datadoghq.com", Strings.AllowEmpty)]
        public void Site(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Site, value));
            var settings = new CIVisibilitySettings(source);

            settings.Site.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void AgentlessUrl(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.AgentlessUrl, value));
            var settings = new CIVisibilitySettings(source);

            settings.AgentlessUrl.Should().Be(expected);
        }

        [Fact]
        public void MaximumAgentlessPayloadSize()
        {
            var source = CreateConfigurationSource();
            var settings = new CIVisibilitySettings(source);

            settings.MaximumAgentlessPayloadSize.Should().Be(5 * 1024 * 1024);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ProxyHttps(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Proxy.ProxyHttps, value));
            var settings = new CIVisibilitySettings(source);

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
            var settings = new CIVisibilitySettings(source);

            settings.ProxyNoProxy.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void IntelligentTestRunnerEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled, value));
            var settings = new CIVisibilitySettings(source);

            settings.IntelligentTestRunnerEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), parameters: new object[] { null })]
        public void TestsSkippingEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.TestsSkippingEnabled, value));
            var settings = new CIVisibilitySettings(source);

            settings.TestsSkippingEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), parameters: new object[] { null })]
        public void CodeCoverageEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverage, value));
            var settings = new CIVisibilitySettings(source);

            settings.CodeCoverageEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CodeCoverageSnkFilePath(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverageSnkFile, value));
            var settings = new CIVisibilitySettings(source);

            settings.CodeCoverageSnkFilePath.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CodeCoveragePath(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoveragePath, value));
            var settings = new CIVisibilitySettings(source);

            settings.CodeCoveragePath.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void CodeCoverageEnableJitOptimizations(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverageEnableJitOptimizations, value));
            var settings = new CIVisibilitySettings(source);

            settings.CodeCoverageEnableJitOptimizations.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), parameters: new object[] { null })]
        public void GitUploadEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.GitUploadEnabled, value));
            var settings = new CIVisibilitySettings(source);

            settings.GitUploadEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void ForceAgentsEvpProxy(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy, value));
            var settings = new CIVisibilitySettings(source);

            settings.ForceAgentsEvpProxy.Should().Be(expected);
        }
    }
}
