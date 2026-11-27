// <copyright file="RemoteConfigurationManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RemoteConfigurationManagerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task ManagedReceiptCommitChecksDiscoveryPublication(bool refreshDuringCommit, bool removesConfigEndpoint)
    {
        var telemetry = new ConfigurationTelemetry();
        var source = new DictionaryConfigurationSource(new Dictionary<string, string>
        {
            ["DD_TRACE_AGENT_URL"] = "http://127.0.0.1:8126",
        });
        var settings = new TracerSettings(source, telemetry);
        const string endpoint = "/v0.7/config";
        var ready = Configuration(endpoint);
        var replacement = removesConfigEndpoint ? Configuration(null) : Configuration(endpoint, "/debugger/v1/input");
        var published = ready;
        var publishOnRead = false;
        var gate = new object();
        var discovery = new Mock<IDiscoveryService>();
        var snapshot = discovery.As<IDiscoverySnapshot>();
        snapshot.SetupGet(service => service.CurrentConfiguration).Returns(() =>
        {
            lock (gate)
            {
                var observed = published;
                PublishPending();
                return observed;
            }
        });
        snapshot.Setup(service => service.TryRunWithCurrentConfiguration(It.IsAny<Func<AgentConfiguration, bool>>()))
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
        subscriptions.Setup(manager => manager.SendRequest(
                         It.IsAny<RcmClientTracer>(),
                         It.IsAny<Func<GetRcmRequest, Task<GetRcmResponse>>>(),
                         It.IsAny<Func<bool>>(),
                         It.IsAny<Action<Action>>()))
                     .Returns((RcmClientTracer _, Func<GetRcmRequest, Task<GetRcmResponse>> _, Func<bool> _, Action<Action> commit) =>
                     {
                         var applied = false;
                         publishOnRead = refreshDuringCommit;
                         commit(() => applied = true);
                         committed.TrySetResult(applied);
                         return Task.CompletedTask;
                     });

        using var manager = RemoteConfigurationManager.Create(
            discovery.Object, RemoteConfigurationSettings.FromSource(source, telemetry), settings, new NullGitMetadataProvider(), subscriptions.Object);
        manager.Start();
        (await committed.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().Be(!removesConfigEndpoint);

        void PublishPending()
        {
            if (publishOnRead)
            {
                publishOnRead = false;
                published = replacement;
            }
        }

        AgentConfiguration Configuration(string configurationEndpoint, string debuggerEndpoint = null) => new(
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
            discoverySettings: settings.Manager.InitialExporterSettings);
    }

    [Fact]
    public void AgentChangesReuseOneDiscoverySubscriptionAndDisposalRemovesIt()
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
            var initialCount = discovery.Callbacks.Count;
            for (var port = 8127; port <= 8129; port++)
            {
                var manual = new ManualInstrumentationConfigurationSource(
                    new Dictionary<string, object> { ["DD_TRACE_AGENT_URL"] = new Uri($"http://127.0.0.1:{port}") },
                    useDefaultSources: true);
                settings.Manager.UpdateManualConfigurationSettings(manual, telemetry).Should().BeTrue();
            }

            discovery.Callbacks.Should().HaveCount(initialCount);
            discovery.Callbacks.Should().ContainSingle();
        }
        finally
        {
            manager.Dispose();
        }

        discovery.Callbacks.Should().BeEmpty();
    }
}
