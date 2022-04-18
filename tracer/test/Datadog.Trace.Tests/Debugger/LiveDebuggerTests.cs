// <copyright file="LiveDebuggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Sink.Models;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class LiveDebuggerTests
{
    // https://datadoghq.atlassian.net/browse/DEBUG-823
    [Fact(Skip = "Non-deterministic test.")]
    public void DebuggerEnabled_ServicesCalled()
    {
        var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
        {
            { ConfigurationKeys.Debugger.DebuggerEnabled, "1" },
            { ConfigurationKeys.Debugger.AgentMode, "1" }
        }));

        var settings = ImmutableDebuggerSettings.Create(tracerSettings);
        var discoveryService = new DiscoveryServiceMock();
        var configurationPoller = new ConfigurationPollerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var debuggerSink = new DebuggerSinkMock();

        LiveDebugger.Create(settings, discoveryService, configurationPoller, lineProbeResolver, debuggerSink);

        Thread.Sleep(100);

        discoveryService.Called.Should().BeTrue();
        configurationPoller.Called.Should().BeTrue();
        debuggerSink.Called.Should().BeTrue();
    }

    [Fact]
    public void DebuggerDisabled_ServicesNotCalled()
    {
        var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
        {
            { ConfigurationKeys.Debugger.DebuggerEnabled, "0" },
        }));

        var settings = ImmutableDebuggerSettings.Create(tracerSettings);
        var discoveryService = new DiscoveryServiceMock();
        var configurationPoller = new ConfigurationPollerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var debuggerSink = new DebuggerSinkMock();

        LiveDebugger.Create(settings, discoveryService, configurationPoller, lineProbeResolver, debuggerSink);

        discoveryService.Called.Should().BeFalse();
        configurationPoller.Called.Should().BeFalse();
        lineProbeResolver.Called.Should().BeFalse();
        debuggerSink.Called.Should().BeFalse();
    }

    private class DiscoveryServiceMock : IDiscoveryService
    {
        public string ProbeConfigurationEndpoint => nameof(ProbeConfigurationEndpoint);

        public string DebuggerEndpoint => nameof(DebuggerEndpoint);

        public string AgentVersion => nameof(AgentVersion);

        internal bool Called { get; private set; }

        public Task<bool> DiscoverAsync()
        {
            Called = true;
            return Task.FromResult(true);
        }
    }

    private class ConfigurationPollerMock : IConfigurationPoller
    {
        internal bool Called { get; private set; }

        public Task StartPollingAsync()
        {
            Called = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
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
}
