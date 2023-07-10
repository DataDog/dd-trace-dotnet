// <copyright file="LiveDebuggerTests.cs" company="Datadog">
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
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class LiveDebuggerTests
{
    [Fact]
    public async Task DebuggerEnabled_ServicesCalled()
    {
        var settings = DebuggerSettings.FromSource(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.Enabled, "1" }, }),
            NullConfigurationTelemetry.Instance);

        var discoveryService = new DiscoveryServiceMock();
        var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var debuggerSink = new DebuggerSinkMock();
        var symbolExtractor = new SymbolExtractorMock();
        var probeStatusPoller = new ProbeStatusPollerMock();
        var updater = ConfigurationUpdater.Create("env", "version");

        var debugger = LiveDebugger.Create(settings, string.Empty, discoveryService, rcmSubscriptionManagerMock, lineProbeResolver, debuggerSink, symbolExtractor, probeStatusPoller, updater, new DogStatsd.NoOpStatsd());
        await debugger.InitializeAsync();

        probeStatusPoller.Called.Should().BeTrue();
        debuggerSink.Called.Should().BeTrue();
        rcmSubscriptionManagerMock.ProductKeys.Contains(RcmProducts.LiveDebugging).Should().BeTrue();
    }

    [Fact]
    public async Task DebuggerDisabled_ServicesNotCalled()
    {
        var settings = DebuggerSettings.FromSource(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.Enabled, "0" }, }),
            NullConfigurationTelemetry.Instance);

        var discoveryService = new DiscoveryServiceMock();
        var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var debuggerSink = new DebuggerSinkMock();
        var symbolExtractor = new SymbolExtractorMock();
        var probeStatusPoller = new ProbeStatusPollerMock();
        var updater = ConfigurationUpdater.Create(string.Empty, string.Empty);

        var debugger = LiveDebugger.Create(settings, string.Empty, discoveryService, rcmSubscriptionManagerMock, lineProbeResolver, debuggerSink, symbolExtractor, probeStatusPoller, updater, new DogStatsd.NoOpStatsd());
        await debugger.InitializeAsync();

        lineProbeResolver.Called.Should().BeFalse();
        debuggerSink.Called.Should().BeFalse();
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
                    agentVersion: "agentVersion",
                    statsEndpoint: "traceStatsEndpoint",
                    dataStreamsMonitoringEndpoint: "dataStreamsMonitoringEndpoint",
                    eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
                    telemetryProxyEndpoint: "telemetryProxyEndpoint",
                    clientDropP0: false));
        }

        public void RemoveSubscription(Action<AgentConfiguration> callback)
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

        public List<ApplyDetails> Update(Dictionary<string, List<RemoteConfiguration>> configByProducts, Dictionary<string, List<RemoteConfigurationPath>> removedConfigsByProduct)
        {
            throw new NotImplementedException();
        }

        public void SetCapability(BigInteger index, bool available)
        {
            throw new NotImplementedException();
        }

        public byte[] GetCapabilities()
        {
            throw new NotImplementedException();
        }

        public GetRcmRequest BuildRequest(RcmClientTracer rcmTracer, string lastPollError)
        {
            throw new NotImplementedException();
        }

        public void ProcessResponse(GetRcmResponse response)
        {
            throw new NotImplementedException();
        }
    }

    private class LineProbeResolverMock : ILineProbeResolver
    {
        internal bool Called { get; private set; }

        public LineProbeResolveResult TryResolveLineProbe(ProbeDefinition probe, out BoundLineProbeLocation location)
        {
            throw new NotImplementedException();
        }
    }

    private class DebuggerSinkMock : IDebuggerSink
    {
        internal bool Called { get; private set; }

        public Task StartFlushingAsync()
        {
            Called = true;
            return Task.CompletedTask;
        }

        public void AddSnapshot(string probeId, string snapshot)
        {
            throw new NotImplementedException();
        }

        public void AddProbeStatus(string probeId, Status status, Exception exception = null, string errorMessage = null)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }

    private class SymbolExtractorMock : ISymbolExtractor
    {
        public void Dispose()
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

        public string[] GetFetchedProbes(string[] candidateProbeIds)
        {
            Called = true;
            return candidateProbeIds;
        }

        public void Dispose()
        {
        }
    }
}
