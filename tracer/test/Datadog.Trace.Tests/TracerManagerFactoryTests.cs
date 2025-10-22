// <copyright file="TracerManagerFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.PlatformHelpers;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer("AWS_LAMBDA_FUNCTION_NAME", "_DD_EXTENSION_PATH")]
public class TracerManagerFactoryTests : IAsyncLifetime
{
    private TracerManager _manager;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _manager?.ShutdownAsync() ?? Task.CompletedTask;

    [Fact]
    public void RemoteConfigIsAvailableByDefault()
    {
        var settings = new TracerSettings();

        settings.IsRemoteConfigurationAvailable.Should().BeTrue();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<RemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<DynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<TracerFlareManager>();
    }

    [Fact]
    public void RemoteConfigIsDisabledInAwsLambda()
    {
        // Lambda.Create() reads environment variables directly, not through TracerSettings
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "something");
        Environment.SetEnvironmentVariable("_DD_EXTENSION_PATH", Path.GetTempFileName());

        // no source needed
        var settings = new TracerSettings();

        settings.IsRemoteConfigurationAvailable.Should().BeFalse();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<NullRemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<NullDynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<NullTracerFlareManager>();
    }

    [Theory]
    [PairwiseData]
    public void RemoteConfigIsDisabledInGcp(bool useDeprecatedEnvVars)
    {
        var source = useDeprecatedEnvVars ?
            GcpHelper.CreateMinimalFirstGenCloudRunFunctionsConfiguration("function-name", "project-id") :
            GcpHelper.CreateMinimalCloudRunFunctionsConfiguration("function-target", "k-service");

        var settings = new TracerSettings(source);

        settings.IsRemoteConfigurationAvailable.Should().BeFalse();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<NullRemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<NullDynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<NullTracerFlareManager>();
    }

    [Fact]
    public void RemoteConfigIsDisabledInAzureAppServices()
    {
        var source = AzureAppServiceHelper.CreateMinimalAzureAppServiceConfiguration("site-name");
        var settings = new TracerSettings(source);

        settings.IsRemoteConfigurationAvailable.Should().BeFalse();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<NullRemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<NullDynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<NullTracerFlareManager>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DiscoveryServiceCanBeDisabled(bool enabled)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.AgentFeaturePollingEnabled, enabled.ToString()));
        var settings = new TracerSettings(source);

        settings.AgentFeaturePollingEnabled.Should().Be(enabled);

        var factory = new TracerManagerFactory();
        var discoveryService = factory.GetDiscoveryService(settings);

        if (enabled)
        {
            discoveryService.Should().BeOfType<DiscoveryService>();
        }
        else
        {
            discoveryService.Should().BeSameAs(NullDiscoveryService.Instance);
        }
    }

    private static TracerManager CreateTracerManager(TracerSettings settings)
    {
        return new TracerManagerFactory().CreateTracerManager(
            settings,
            Mock.Of<IAgentWriter>(),
            Mock.Of<ITraceSampler>(),
            Mock.Of<IScopeManager>(),
            Mock.Of<IDogStatsd>(),
            BuildRuntimeMetrics(),
            BuildLogSubmissionManager(),
            Mock.Of<ITelemetryController>(),
            Mock.Of<IDiscoveryService>(),
            new DataStreamsManager("env", "service", Mock.Of<IDataStreamsWriter>(), isInDefaultState: false, processTags: null),
            remoteConfigurationManager: null,
            dynamicConfigurationManager: null,
            tracerFlareManager: null,
            spanEventsManager: null,
            settingsManager: null);

        static DirectLogSubmissionManager BuildLogSubmissionManager()
            => DirectLogSubmissionManager.Create(
                previous: null,
                settings: new TracerSettings(NullConfigurationSource.Instance),
                directLogSettings: new TracerSettings().LogSubmissionSettings,
                azureAppServiceSettings: null,
                serviceName: "test",
                env: "test",
                serviceVersion: "test",
                gitMetadataTagsProvider: Mock.Of<IGitMetadataTagsProvider>());

        static RuntimeMetricsWriter BuildRuntimeMetrics()
            => new(Mock.Of<IDogStatsd>(), TimeSpan.FromMinutes(1), inAzureAppServiceContext: false, (_, _, _) => Mock.Of<IRuntimeMetricsListener>());
    }

    private static IConfigurationSource CreateConfigurationSource(params (string Key, string Value)[] values)
    {
        var config = new NameValueCollection();

        foreach (var (key, value) in values)
        {
            config.Add(key, value);
        }

        return new NameValueConfigurationSource(config);
    }
}
