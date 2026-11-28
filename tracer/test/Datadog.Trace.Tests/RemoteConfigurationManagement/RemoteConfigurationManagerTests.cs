// <copyright file="RemoteConfigurationManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RemoteConfigurationManagerTests
{
    public enum DiscoveryRefresh
    {
        None,
        KeepEndpoint,
        RemoveEndpoint,
        RestoreEndpoint,
    }

    [Theory]
    [CombinatorialData]
    public async Task ManagedReceiptCommitChecksDiscoveryPublication(DiscoveryRefresh refresh)
    {
        var telemetry = new ConfigurationTelemetry();
        var source = new DictionaryConfigurationSource(new Dictionary<string, string>
        {
            ["DD_TRACE_AGENT_URL"] = "http://127.0.0.1:8126",
        });
        var settings = new TracerSettings(source, telemetry);
        const string endpoint = "/v0.7/config";
        var ready = Configuration(settings.Manager.InitialExporterSettings, endpoint) with { RemoteConfigurationEndpointGeneration = 1 };
        var replacement = refresh switch
        {
            DiscoveryRefresh.RemoveEndpoint => Configuration(settings.Manager.InitialExporterSettings, null) with { RemoteConfigurationEndpointGeneration = 2 },
            DiscoveryRefresh.RestoreEndpoint => ready with { RemoteConfigurationEndpointGeneration = 3 },
            _ => Configuration(settings.Manager.InitialExporterSettings, endpoint, "/debugger/v1/input") with { RemoteConfigurationEndpointGeneration = 1 },
        };
        var keepsEndpoint = refresh is DiscoveryRefresh.None or DiscoveryRefresh.KeepEndpoint;
        var published = ready;
        var publishOnRead = false;
        var gate = new object();
        var discovery = new Mock<IDiscoveryService>();
        discovery.SetupGet(service => service.CurrentConfiguration).Returns(() =>
        {
            lock (gate)
            {
                var observed = published;
                PublishPending();
                return observed;
            }
        });
        discovery.Setup(service => service.TryRunWithCurrentConfiguration(It.IsAny<Func<AgentConfiguration, bool>>()))
                .Returns((Func<AgentConfiguration, bool> apply) =>
                {
                    lock (gate)
                    {
                        PublishPending();
                        return apply(published);
                    }
                });

        var committed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriptions = new Mock<IRcmSubscriptionManager>();
        subscriptions.SetupGet(manager => manager.HasAnySubscription).Returns(true);
        ManagedRcmPoll poll = (_, _, _, commit) =>
        {
            var applied = false;
            publishOnRead = refresh != DiscoveryRefresh.None;
            commit(() => applied = true);
            committed.TrySetResult(applied);
            return Task.CompletedTask;
        };
        subscriptions.Setup(manager => manager.CreateManagedPoller()).Returns(poll);

        using var manager = RemoteConfigurationManager.Create(
            discovery.Object, RemoteConfigurationSettings.FromSource(source, telemetry), settings, new NullGitMetadataProvider(), subscriptions.Object);
        manager.Start();
        (await committed.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().Be(keepsEndpoint);

        void PublishPending()
        {
            if (publishOnRead)
            {
                publishOnRead = false;
                published = replacement;
            }
        }
    }

    [Fact]
    public void AgentChangesReadPublishedDiscoveryWithoutRegisteringCallbacks()
    {
        var telemetry = new ConfigurationTelemetry();
        var source = new DictionaryConfigurationSource(new Dictionary<string, string>
        {
            ["DD_TRACE_AGENT_URL"] = "http://127.0.0.1:8126",
        });
        var settings = new TracerSettings(source, telemetry);
        var discovery = new DiscoveryServiceMock();
        var manager = RemoteConfigurationManager.Create(
            discovery, RemoteConfigurationSettings.FromSource(source, telemetry), settings, new NullGitMetadataProvider(), new RcmSubscriptionManager());
        try
        {
            for (var port = 8127; port <= 8129; port++)
            {
                var manual = new ManualInstrumentationConfigurationSource(
                    new Dictionary<string, object> { ["DD_TRACE_AGENT_URL"] = new Uri($"http://127.0.0.1:{port}") },
                    useDefaultSources: true);
                settings.Manager.UpdateManualConfigurationSettings(manual, telemetry).Should().BeTrue();
            }

            discovery.Callbacks.Should().BeEmpty();
        }
        finally
        {
            manager.Dispose();
        }

        discovery.Callbacks.Should().BeEmpty();
    }

    private static AgentConfiguration Configuration(ExporterSettings exporter, string configurationEndpoint, string debuggerEndpoint = null) => new(
        configurationEndpoint: configurationEndpoint,
        debuggerEndpoint: debuggerEndpoint,
        debuggerV2Endpoint: null,
        diagnosticsEndpoint: null,
        symbolDbEndpoint: null,
        agentVersion: null,
        statsEndpoint: null,
        dataStreamsMonitoringEndpoint: null,
        eventPlatformProxyEndpoint: null,
        telemetryProxyEndpoint: null,
        tracerFlareEndpoint: null,
        containerTagsHash: null,
        clientDropP0: false,
        spanMetaStructs: false,
        spanEvents: false,
        discoverySettings: exporter);

    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class PublicationOwnershipTests
    {
        [Fact]
        public async Task SharedWorkerRejectsThePreviousOwnersResponse()
        {
            var telemetry = new ConfigurationTelemetry();
            var source = new DictionaryConfigurationSource(new Dictionary<string, string>
            {
                ["DD_TRACE_AGENT_URL"] = "http://127.0.0.1:8126",
                ["DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS"] = "0.01",
            });
            var settings = new TracerSettings(source, telemetry);
            var route = Configuration(settings.Manager.InitialExporterSettings, "/v0.7/config");
            var discovery = new Mock<IDiscoveryService>();
            discovery.SetupGet(service => service.CurrentConfiguration).Returns(route);
            discovery.Setup(service => service.TryRunWithCurrentConfiguration(It.IsAny<Func<AgentConfiguration, bool>>()))
                     .Returns((Func<AgentConfiguration, bool> apply) => apply(route));

            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var previous = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var successor = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var polls = 0;
            var subscriptions = new Mock<IRcmSubscriptionManager>();
            subscriptions.SetupGet(manager => manager.HasAnySubscription).Returns(true);
            ManagedRcmPoll poll = async (_, _, _, commit) =>
            {
                var first = Interlocked.Increment(ref polls) == 1;
                if (first)
                {
                    pending.SetResult(true);
                    await release.Task;
                }

                var published = false;
                commit(() => published = true);
                (first ? previous : successor).TrySetResult(published);
            };
            subscriptions.Setup(manager => manager.CreateManagedPoller()).Returns(poll);
            using var manager = RemoteConfigurationManager.Create(
                discovery.Object, RemoteConfigurationSettings.FromSource(source, telemetry), settings, new NullGitMetadataProvider(), subscriptions.Object);

            try
            {
                StartOwner();
                await pending.Task.WaitAsync(TimeSpan.FromSeconds(10));
                StartOwner();
                release.SetResult(true);

                (await previous.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeFalse();
                (await successor.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
            }
            finally
            {
                release.TrySetResult(true);
                await manager.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }

            void StartOwner()
            {
                var owner = new TracerManager(
                    settings,
                    agentWriter: null,
                    scopeManager: null,
                    statsd: null,
                    runtimeMetricsWriter: null,
                    directLogSubmission: null,
                    telemetry: null,
                    discovery.Object,
                    dataStreamsManager: null,
                    new NullGitMetadataProvider(),
                    traceSampler: null,
                    spanSampler: null,
                    manager,
                    new NullDynamicConfigurationManager(),
                    new NullTracerFlareManager(),
                    new NullSpanEventsManager(),
                    featureFlagsModule: null,
                    serviceRemappingHash: null);
                TracerManager.UnsafeReplaceGlobalManager(owner);
                owner.Start();
            }
        }
    }
}
