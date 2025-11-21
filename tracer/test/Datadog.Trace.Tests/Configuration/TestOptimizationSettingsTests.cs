// <copyright file="TestOptimizationSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class TestOptimizationSettingsTests : SettingsTestsBase
    {
        private static readonly string ExpectedExcludedSession = "/session/FakeSessionIdForPollingPurposes".ToUpperInvariant();

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Agentless(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.AgentlessEnabled, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.Agentless.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), false)]
        public void Logs(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.Logs, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.Logs.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ApiKey(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ApiKey, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.ApiKey.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases), "datadoghq.com", Strings.AllowEmpty)]
        public void Site(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Site, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.Site.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void AgentlessUrl(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.AgentlessUrl, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.AgentlessUrl.Should().Be(expected);
        }

        [Fact]
        public void MaximumAgentlessPayloadSize()
        {
            var source = CreateConfigurationSource();
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.MaximumAgentlessPayloadSize.Should().Be(5 * 1024 * 1024);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ProxyHttps(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.Proxy.ProxyHttps, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

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
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.ProxyNoProxy.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void IntelligentTestRunnerEnabled(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.IntelligentTestRunnerEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(NullableBooleanTestCases), null)]
        public void TestsSkippingEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.TestsSkippingEnabled, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.TestsSkippingEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(NullableBooleanTestCases), null)]
        public void CodeCoverageEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverage, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.CodeCoverageEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CodeCoverageSnkFilePath(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverageSnkFile, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.CodeCoverageSnkFilePath.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void CodeCoveragePath(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoveragePath, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.CodeCoveragePath.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(BooleanTestCases), true)]
        public void CodeCoverageEnableJitOptimizations(string value, bool expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.CodeCoverageEnableJitOptimizations, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.CodeCoverageEnableJitOptimizations.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(NullableBooleanTestCases), null)]
        public void GitUploadEnabled(string value, bool? expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.GitUploadEnabled, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.GitUploadEnabled.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(StringTestCases))]
        public void ForceAgentsEvpProxy(string value, string expected)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy, value));
            var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);

            settings.ForceAgentsEvpProxy.Should().Be(expected);
        }

        [Theory]
        [InlineData("some-service", "true")]
        [InlineData(null, "false")]
        public void AddsUserProvidedTestServiceTagToGlobalTags(string serviceName, string expectedTag)
        {
            var source = CreateConfigurationSource((ConfigurationKeys.ServiceName, serviceName));

            var ciVisSettings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);
            var tracerSettings = ciVisSettings.InitializeTracerSettings(source);

            tracerSettings.GlobalTags.Should()
                          .ContainKey(Datadog.Trace.Ci.Tags.CommonTags.UserProvidedTestServiceTag)
                          .WhoseValue.Should()
                          .Be(expectedTag);
        }

        [Fact]
        public void ServiceNameIsNormalized()
        {
            var originalName = "My Service Name!";
            var normalizedName = "my_service_name";

            var source = CreateConfigurationSource((ConfigurationKeys.ServiceName, originalName));

            var ciVisSettings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);
            var tracerSettings = ciVisSettings.InitializeTracerSettings(source);

            tracerSettings.ServiceName.Should().Be(normalizedName);
        }

        [Fact]
        public void AddsFakeSessionToExcludedHttpClientUrls()
        {
            var source = CreateConfigurationSource();

            var ciVisSettings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);
            var tracerSettings = ciVisSettings.InitializeTracerSettings(source);

            tracerSettings.HttpClientExcludedUrlSubstrings
                          .Should()
                          .Contain(ExpectedExcludedSession);
        }

        [Fact]
        public void AddsFakeSessionToExcludedHttpClientUrls_WhenUrlsAlreadyExist()
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.HttpClientExcludedUrlSubstrings, "/some-url/path"));

            var ciVisSettings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);
            var tracerSettings = ciVisSettings.InitializeTracerSettings(source);

            tracerSettings.HttpClientExcludedUrlSubstrings
                          .Should()
                          .Contain(ExpectedExcludedSession);
        }

        [Fact]
        public void AddsFakeSessionToExcludedHttpClientUrls_WhenRunningInAas()
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.AzureAppService.AzureAppServicesContextKey, "true"),
                (ConfigurationKeys.HttpClientExcludedUrlSubstrings, "/some-url/path"));

            var ciVisSettings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);
            var tracerSettings = ciVisSettings.InitializeTracerSettings(source);

            tracerSettings.HttpClientExcludedUrlSubstrings
                          .Should()
                          .Contain(ExpectedExcludedSession);
        }

        [Fact]
        public void WhenLogsEnabled_AddsDirectSubmission()
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.CIVisibility.Logs, "true"));

            var ciVisSettings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);
            var tracerSettings = ciVisSettings.InitializeTracerSettings(source);

            tracerSettings.LogSubmissionSettings
                          .EnabledIntegrationNames
                          .Should()
                          .Contain(nameof(IntegrationId.XUnit));
            tracerSettings.LogSubmissionSettings.BatchPeriod.Should().Be(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void WhenLogsNotEnabled_DoesNotAddDirectSubmission()
        {
            var source = CreateConfigurationSource();

            var ciVisSettings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);
            var tracerSettings = ciVisSettings.InitializeTracerSettings(source);

            tracerSettings.LogSubmissionSettings
                          .EnabledIntegrationNames
                          .Should()
                          .NotContain(nameof(IntegrationId.XUnit));
        }
    }
}
