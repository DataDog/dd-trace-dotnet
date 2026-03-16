// <copyright file="DynamicInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class DynamicInstrumentationTests
{
    [Fact]
    public async Task DynamicInstrumentationEnabled_ServicesCalled()
    {
        var settings = DebuggerSettings.FromSource(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
            NullConfigurationTelemetry.Instance);

        var discoveryService = new DiscoveryServiceMock();
        var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var snapshotUploader = new SnapshotUploaderMock();
        var logUploader = new LogUploaderMock();
        var diagnosticsUploader = new UploaderMock();
        var probeStatusPoller = new ProbeStatusPollerMock();
        var updater = ConfigurationUpdater.Create("env", "version", 0);

        var debugger = new DynamicInstrumentation(settings, discoveryService, rcmSubscriptionManagerMock, lineProbeResolver, snapshotUploader, logUploader, diagnosticsUploader, probeStatusPoller, updater, NoOpStatsd.Instance);
        debugger.Initialize();

        // Wait for async initialization to complete
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        while (!debugger.IsInitialized && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }

        discoveryService.Called.Should().BeTrue();
        debugger.IsInitialized.Should().BeTrue("Dynamic instrumentation should be initialized");

        probeStatusPoller.Called.Should().BeTrue();
        snapshotUploader.Called.Should().BeTrue();
        diagnosticsUploader.Called.Should().BeTrue();
        rcmSubscriptionManagerMock.ProductKeys.Contains(RcmProducts.LiveDebugging).Should().BeTrue();
    }

    [Fact]
    public void DynamicInstrumentationDisabled_ServicesNotCalled()
    {
        var settings = DebuggerSettings.FromSource(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "0" }, }),
            NullConfigurationTelemetry.Instance);

        var discoveryService = new DiscoveryServiceMock();
        var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var snapshotUploader = new SnapshotUploaderMock();
        var logUploader = new LogUploaderMock();
        var diagnosticsUploader = new UploaderMock();
        var probeStatusPoller = new ProbeStatusPollerMock();
        var updater = ConfigurationUpdater.Create(string.Empty, string.Empty, 0);

        var debugger = new DynamicInstrumentation(settings, discoveryService, rcmSubscriptionManagerMock, lineProbeResolver, snapshotUploader, logUploader, diagnosticsUploader, probeStatusPoller, updater, NoOpStatsd.Instance);
        debugger.Initialize();
        lineProbeResolver.Called.Should().BeFalse();
        probeStatusPoller.Called.Should().BeFalse();
        snapshotUploader.Called.Should().BeFalse();
        diagnosticsUploader.Called.Should().BeFalse();
        probeStatusPoller.Called.Should().BeFalse();
        rcmSubscriptionManagerMock.ProductKeys.Contains(RcmProducts.LiveDebugging).Should().BeFalse();
    }

    private class DiscoveryServiceMock : IDiscoveryService
    {
        internal bool Called { get; private set; }

        public void SubscribeToChanges(Action<AgentConfiguration> callback)
        {
            Called = true;
            callback(
                new AgentConfiguration(
                    configurationEndpoint: "configurationEndpoint",
                    debuggerEndpoint: "debuggerEndpoint",
                    debuggerV2Endpoint: "debuggerV2Endpoint",
                    diagnosticsEndpoint: "diagnosticsEndpoint",
                    symbolDbEndpoint: "symbolDbEndpoint",
                    agentVersion: "agentVersion",
                    statsEndpoint: "traceStatsEndpoint",
                    dataStreamsMonitoringEndpoint: "dataStreamsMonitoringEndpoint",
                    eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
                    telemetryProxyEndpoint: "telemetryProxyEndpoint",
                    tracerFlareEndpoint: "tracerFlareEndpoint",
                    clientDropP0: false,
                    spanMetaStructs: true,
                    spanEvents: true));
        }

        public void RemoveSubscription(Action<AgentConfiguration> callback)
        {
        }

        public void SetCurrentConfigStateHash(string configStateHash)
        {
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private class RcmSubscriptionManagerMock : IRcmSubscriptionManager
    {
        public bool HasAnySubscription { get; }

        public ICollection<string> ProductKeys { get; } = new List<string>();

        public void SubscribeToChanges(ISubscription subscription)
        {
            foreach (var productKey in subscription.ProductKeys)
            {
                ProductKeys.Add(productKey);
            }
        }

        public void Replace(ISubscription oldSubscription, ISubscription newSubscription)
        {
            foreach (var productKey in oldSubscription.ProductKeys)
            {
                ProductKeys.Remove(productKey);
            }

            foreach (var productKey in newSubscription.ProductKeys)
            {
                ProductKeys.Add(productKey);
            }
        }

        public void Unsubscribe(ISubscription subscription)
        {
            foreach (var productKey in subscription.ProductKeys)
            {
                ProductKeys.Remove(productKey);
            }
        }

        public void SetCapability(BigInteger index, bool available)
        {
            throw new NotImplementedException();
        }

        public byte[] GetCapabilities()
        {
            throw new NotImplementedException();
        }

        public Task SendRequest(RcmClientTracer rcmTracer, Func<GetRcmRequest, Task<GetRcmResponse>> callback)
        {
            throw new NotImplementedException();
        }
    }

    private class LineProbeResolverMock : ILineProbeResolver
    {
        internal bool Called { get; private set; }

        public LineProbeResolveResult TryResolveLineProbe(ProbeDefinition probe, out LineProbeResolver.BoundLineProbeLocation location)
        {
            throw new NotImplementedException();
        }
    }

    private class UploaderMock : IDebuggerUploader
    {
        internal bool Called { get; private set; }

        public Task StartFlushingAsync()
        {
            Called = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private class SnapshotUploaderMock : UploaderMock, ISnapshotUploader
    {
        public void Add(string probeId, string snapshot)
        {
        }
    }

    private class LogUploaderMock : UploaderMock, ISnapshotUploader
    {
        public void Add(string probeId, string snapshot)
        {
        }
    }

    private class ProbeStatusPollerMock : IProbeStatusPoller
    {
        internal bool Called { get; private set; }

        public void StartPolling()
        {
            Called = true;
        }

        public void AddProbes(FetchProbeStatus[] newProbes)
        {
            Called = true;
        }

        public void RemoveProbes(string[] removeProbes)
        {
            Called = true;
        }

        public void UpdateProbes(string[] probeIds, FetchProbeStatus[] newProbeStatuses)
        {
            Called = true;
        }

        public void UpdateProbe(string probeId, FetchProbeStatus newProbeStatus)
        {
            Called = true;
        }

        public string[] GetBoundedProbes()
        {
            Called = true;
            return [];
        }

        public void Dispose()
        {
        }
    }
}
