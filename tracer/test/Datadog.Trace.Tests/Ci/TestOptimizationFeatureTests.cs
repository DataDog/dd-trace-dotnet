// <copyright file="TestOptimizationFeatureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class TestOptimizationFeatureTests : SettingsTestsBase
{
    [Fact]
    public void InvalidKnownTestsResponseDisablesKnownTestsAndEarlyFlakeDetection()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.KnownTestsEnabled, "true"),
            (ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "true"));
        var remoteSettings = TestOptimizationClient.CreateSettingsResponseFromTestOptimizationSettings(settings, tracerManagement: null);
        var client = new TestOptimizationClientStub(knownTestsResponse: default);

        var knownTestsFeature = TestOptimizationKnownTestsFeature.Create(settings, remoteSettings, client);
        var earlyFlakeDetectionFeature = TestOptimizationEarlyFlakeDetectionFeature.Create(settings, remoteSettings, knownTestsFeature);

        knownTestsFeature.Enabled.Should().BeFalse();
        settings.KnownTestsEnabled.Should().BeFalse();
        settings.EarlyFlakeDetectionEnabled.Should().BeFalse();
        earlyFlakeDetectionFeature.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ExplicitEmptyKnownTestsPayloadKeepsKnownTestsAndEarlyFlakeDetectionEnabled()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.KnownTestsEnabled, "true"),
            (ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "true"));
        var remoteSettings = TestOptimizationClient.CreateSettingsResponseFromTestOptimizationSettings(settings, tracerManagement: null);
        var client = new TestOptimizationClientStub(
            new TestOptimizationClient.KnownTestsResponse(new TestOptimizationClient.KnownTestsResponse.KnownTestsModules()));

        var knownTestsFeature = TestOptimizationKnownTestsFeature.Create(settings, remoteSettings, client);
        var earlyFlakeDetectionFeature = TestOptimizationEarlyFlakeDetectionFeature.Create(settings, remoteSettings, knownTestsFeature);

        knownTestsFeature.Enabled.Should().BeTrue();
        settings.KnownTestsEnabled.Should().BeTrue();
        settings.EarlyFlakeDetectionEnabled.Should().BeTrue();
        earlyFlakeDetectionFeature.Enabled.Should().BeTrue();
    }

    [Fact]
    public void InvalidKnownTestsResponseDisablesEarlyFlakeDetectionWhenQueriedFirst()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.KnownTestsEnabled, "true"),
            (ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "true"));
        var remoteSettings = TestOptimizationClient.CreateSettingsResponseFromTestOptimizationSettings(settings, tracerManagement: null);
        var client = new TestOptimizationClientStub(knownTestsResponse: default);

        var knownTestsFeature = TestOptimizationKnownTestsFeature.Create(settings, remoteSettings, client);
        var earlyFlakeDetectionFeature = TestOptimizationEarlyFlakeDetectionFeature.Create(settings, remoteSettings, knownTestsFeature);

        earlyFlakeDetectionFeature.Enabled.Should().BeFalse();
        settings.KnownTestsEnabled.Should().BeFalse();
        settings.EarlyFlakeDetectionEnabled.Should().BeFalse();
        knownTestsFeature.Enabled.Should().BeFalse();
    }

    private static TestOptimizationSettings CreateSettings(params (string Key, string Value)[] values)
        => new(CreateConfigurationSource(values), NullConfigurationTelemetry.Instance);

    private sealed class TestOptimizationClientStub(TestOptimizationClient.KnownTestsResponse knownTestsResponse) : ITestOptimizationClient
    {
        public Task<TestOptimizationClient.SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false)
            => Task.FromResult(default(TestOptimizationClient.SettingsResponse));

        public Task<TestOptimizationClient.KnownTestsResponse> GetKnownTestsAsync()
            => Task.FromResult(knownTestsResponse);

        public Task<TestOptimizationClient.SearchCommitResponse> GetCommitsAsync()
            => Task.FromResult(default(TestOptimizationClient.SearchCommitResponse));

        public Task<TestOptimizationClient.SkippableTestsResponse> GetSkippableTestsAsync()
            => Task.FromResult(default(TestOptimizationClient.SkippableTestsResponse));

        public Task<long> SendPackFilesAsync(string commitSha, string[] commitsToInclude, string[] commitsToExclude)
            => Task.FromResult(0L);

        public Task<long> UploadRepositoryChangesAsync()
            => Task.FromResult(0L);

        public Task<TestOptimizationClient.TestManagementResponse> GetTestManagementTests()
            => Task.FromResult(default(TestOptimizationClient.TestManagementResponse));
    }
}
