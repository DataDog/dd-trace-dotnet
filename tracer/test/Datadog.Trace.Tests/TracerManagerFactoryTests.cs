// <copyright file="TracerManagerFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer("DD_AZURE_APP_SERVICES", "FUNCTIONS_WORKER_RUNTIME", "FUNCTION_TARGET", "AWS_LAMBDA_FUNCTION_NAME", "K_SERVICE", "_DD_EXTENSION_PATH")]
public class TracerManagerFactoryTests : IAsyncLifetime
{
    private TracerManager _manager = null;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _manager?.ShutdownAsync() ?? Task.CompletedTask;

    [Fact]
    public void RemoteConfigIsAvailableByDefault()
    {
        var settings = TracerSettings.FromDefaultSourcesInternal().Build();
        settings.IsRemoteConfigurationAvailable.Should().BeTrue();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<RemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<DynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<TracerFlareManager>();
    }

    [Theory]
    [InlineData("DD_AZURE_APP_SERVICES", "1")]
    [InlineData("FUNCTIONS_WORKER_RUNTIME", "dotnet")]
    [InlineData("FUNCTION_TARGET", "something")]
    [InlineData("AWS_LAMBDA_FUNCTION_NAME", "something")]
    public void RemoteConfigIsDisabledInServerlessScenarios(string key, string value)
    {
        Environment.SetEnvironmentVariable(key, value);

        // These do nothing on their own but combine later to give an effect
        Environment.SetEnvironmentVariable("K_SERVICE", "something");
        Environment.SetEnvironmentVariable("_DD_EXTENSION_PATH", Path.GetTempFileName());

        var settings = TracerSettings.FromDefaultSourcesInternal().Build();

        settings.IsRemoteConfigurationAvailable.Should().BeFalse();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<NullRemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<NullDynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<NullTracerFlareManager>();
    }

    private static TracerManager CreateTracerManager(ImmutableTracerSettings settings)
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
            new DataStreamsManager("env", "service", Mock.Of<IDataStreamsWriter>()),
            remoteConfigurationManager: null,
            dynamicConfigurationManager: null,
            tracerFlareManager: null);

        static DirectLogSubmissionManager BuildLogSubmissionManager()
            => DirectLogSubmissionManager.Create(
                previous: null,
                settings: new ImmutableTracerSettings(NullConfigurationSource.Instance),
                directLogSettings: ImmutableDirectLogSubmissionSettings.Create(new TracerSettings()),
                azureAppServiceSettings: null,
                serviceName: "test",
                env: "test",
                serviceVersion: "test",
                gitMetadataTagsProvider: Mock.Of<IGitMetadataTagsProvider>());

        static RuntimeMetricsWriter BuildRuntimeMetrics()
            => new(Mock.Of<IDogStatsd>(), TimeSpan.FromMinutes(1), inAzureAppServiceContext: false, (_, _, _) => Mock.Of<IRuntimeMetricsListener>());
    }
}
