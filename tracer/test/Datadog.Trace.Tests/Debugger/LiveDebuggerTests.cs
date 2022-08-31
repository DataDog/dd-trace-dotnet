// <copyright file="LiveDebuggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.RemoteConfigurationManagement;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class LiveDebuggerTests
{
    [Fact]
    public async Task DebuggerEnabled_ServicesCalled()
    {
        var settings = DebuggerSettings.FromSource(new NameValueConfigurationSource(new()
        {
            { ConfigurationKeys.Debugger.Enabled, "1" },
        }));

        var discoveryService = new DiscoveryServiceMock();
        var managerMock = new RemoteConfigurationManagerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var debuggerSink = new DebuggerSinkMock();
        var probeStatusPoller = new ProbeStatusPollerMock();
        var updater = ConfigurationUpdater.Create("env", "version");

        var debugger = LiveDebugger.Create(settings, string.Empty, discoveryService, managerMock, lineProbeResolver, debuggerSink, probeStatusPoller, updater);
        await debugger.InitializeAsync();

        probeStatusPoller.Called.Should().BeTrue();
        debuggerSink.Called.Should().BeTrue();
        managerMock.Products.ContainsKey(LiveDebugger.Instance.Product.Name).Should().BeTrue();
    }

    [Fact]
    public async Task DebuggerDisabled_ServicesNotCalled()
    {
        var settings = DebuggerSettings.FromSource(new NameValueConfigurationSource(new()
        {
            { ConfigurationKeys.Debugger.Enabled, "0" },
        }));

        var discoveryService = new DiscoveryServiceMock();
        var managerMock = new RemoteConfigurationManagerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var debuggerSink = new DebuggerSinkMock();
        var probeStatusPoller = new ProbeStatusPollerMock();
        var updater = ConfigurationUpdater.Create(string.Empty, string.Empty);

        var debugger = LiveDebugger.Create(settings, string.Empty, discoveryService, managerMock, lineProbeResolver, debuggerSink, probeStatusPoller, updater);
        await debugger.InitializeAsync();

        lineProbeResolver.Called.Should().BeFalse();
        debuggerSink.Called.Should().BeFalse();
        probeStatusPoller.Called.Should().BeFalse();
        managerMock.Products.ContainsKey(LiveDebugger.Instance.Product.Name).Should().BeFalse();
    }

    private class DiscoveryServiceMock : IDiscoveryService
    {
        public string ConfigurationEndpoint => nameof(ConfigurationEndpoint);

        public string DebuggerEndpoint => nameof(DebuggerEndpoint);

        public string StatsEndpoint => nameof(StatsEndpoint);

        public string AgentVersion => nameof(AgentVersion);

        public bool? ClientDropP0s => false;

        internal bool Called { get; private set; }

        public Task<bool> DiscoverAsync()
        {
            Called = true;
            return Task.FromResult(true);
        }
    }

    private class RemoteConfigurationManagerMock : IRemoteConfigurationManager
    {
        internal bool Called { get; private set; }

        internal Dictionary<string, Product> Products { get; private set; } = new();

        public Task StartPollingAsync()
        {
            Called = true;
            return Task.CompletedTask;
        }

        public void RegisterProduct(Product product)
        {
            Products.Add(product.Name, product);
        }

        public void UnregisterProduct(string productName)
        {
            Products.Remove(productName);
        }
    }

    private class LineProbeResolverMock : ILineProbeResolver
    {
        internal bool Called { get; private set; }

        public void OnDomainUnloaded()
        {
            Called = true;
        }

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

        public void AddSnapshot(string snapshot)
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

    private class ProbeStatusPollerMock : IProbeStatusPoller
    {
        internal bool Called { get; private set; }

        public void StartPolling()
        {
            Called = true;
        }

        public void AddProbes(string[] newProbes)
        {
            Called = true;
        }

        public void RemoveProbes(string[] newProbes)
        {
            Called = true;
        }

        public void Dispose()
        {
        }
    }
}
