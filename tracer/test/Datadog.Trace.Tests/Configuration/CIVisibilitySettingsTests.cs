// <copyright file="CIVisibilitySettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
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

            Assert.Equal(expected, settings.Enabled);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Agentless(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.AgentlessEnabled, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.Agentless);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Logs(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.Logs, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.Logs);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ApiKey(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApiKey, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.ApiKey);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ApplicationKey(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApplicationKey, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.ApplicationKey);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "datadoghq.com", Strings.AllowEmpty)]
        public void Site(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Site, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.Site);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void AgentlessUrl(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.AgentlessUrl, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.AgentlessUrl);
        }

        [Fact]
        public void MaximumAgentlessPayloadSize()
        {
            var source = CreateConfigurationSource();
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(5 * 1024 * 1024, settings.MaximumAgentlessPayloadSize);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ProxyHttps(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Proxy.ProxyHttps, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.ProxyHttps);
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

            Assert.Equal(expected, settings.ProxyNoProxy);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void IntelligentTestRunnerEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.IntelligentTestRunnerEnabled);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), parameters: new object[] { null })]
        public void TestsSkippingEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.TestsSkippingEnabled, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.TestsSkippingEnabled);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), parameters: new object[] { null })]
        public void CodeCoverageEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverage, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.CodeCoverageEnabled);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CodeCoverageSnkFilePath(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverageSnkFile, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.CodeCoverageSnkFilePath);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CodeCoveragePath(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoveragePath, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.CodeCoveragePath);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void CodeCoverageEnableJitOptimizations(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverageEnableJitOptimizations, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.CodeCoverageEnableJitOptimizations);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), parameters: new object[] { null })]
        public void GitUploadEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.GitUploadEnabled, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.GitUploadEnabled);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void ForceAgentsEvpProxy(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy, value));
            var settings = new CIVisibilitySettings(source);

            Assert.Equal(expected, settings.ForceAgentsEvpProxy);
        }
    }
}
