// <copyright file="LiveDebuggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.RemoteConfigurationManagement;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class LiveDebuggerTests
{
    private readonly Mock<IDiscoveryService> _discoveryService;
    private readonly Mock<IRemoteConfigurationManager> _remoteConfigurationManager;
    private readonly Mock<ILineProbeResolver> _lineProbeResolver;
    private readonly Mock<IDebuggerSink> _debuggerSink;
    private readonly Mock<IProbeStatusPoller> _probeStatusPoller;
    private readonly ConfigurationUpdater _configurationUpdater;

    public LiveDebuggerTests()
    {
        _discoveryService = new Mock<IDiscoveryService>();
        _discoveryService
           .Setup(service => service.SubscribeToChanges(It.IsAny<Action<AgentConfiguration>>()))
           .Callback(
                (Action<AgentConfiguration> callback) => callback(
                    new AgentConfiguration(
                        configurationEndpoint: "configurationEndpoint",
                        debuggerEndpoint: "debuggerEndpoint",
                        agentVersion: "agentVersion",
                        statsEndpoint: "traceStatsEndpoint",
                        dataStreamsMonitoringEndpoint: "dataStreamsMonitoringEndpoint",
                        clientDropP0: false)));

        _remoteConfigurationManager = new Mock<IRemoteConfigurationManager>();
        _lineProbeResolver = new Mock<ILineProbeResolver>();
        _debuggerSink = new Mock<IDebuggerSink>();
        _probeStatusPoller = new Mock<IProbeStatusPoller>();
        _configurationUpdater = ConfigurationUpdater.Create("env", "version");
    }

    [Fact]
    public async Task DebuggerEnabled_ServicesCalled()
    {
        var settings = DebuggerSettings.FromSource(new NameValueConfigurationSource(new()
        {
            { ConfigurationKeys.Debugger.Enabled, "1" },
        }));

        var debugger = LiveDebugger.Create(settings, string.Empty, _discoveryService.Object, _remoteConfigurationManager.Object, _lineProbeResolver.Object, _debuggerSink.Object, _probeStatusPoller.Object, _configurationUpdater);
        await debugger.InitializeAsync();

        _discoveryService.Verify(service => service.SubscribeToChanges(It.IsAny<Action<AgentConfiguration>>()), Times.Once());
        _remoteConfigurationManager.Verify(rcm => rcm.RegisterProduct(It.IsAny<LiveDebuggerProduct>()), Times.Once());
        _debuggerSink.Verify(debuggerSink => debuggerSink.StartFlushingAsync(), Times.Once());
        _probeStatusPoller.Verify(poller => poller.StartPolling(), Times.Once());
    }

    [Fact]
    public async Task DebuggerDisabled_ServicesNotCalled()
    {
        var settings = DebuggerSettings.FromSource(new NameValueConfigurationSource(new()
        {
            { ConfigurationKeys.Debugger.Enabled, "0" },
        }));

        var debugger = LiveDebugger.Create(settings, string.Empty, _discoveryService.Object, _remoteConfigurationManager.Object, _lineProbeResolver.Object, _debuggerSink.Object, _probeStatusPoller.Object, _configurationUpdater);
        await debugger.InitializeAsync();

        _discoveryService.Verify(service => service.SubscribeToChanges(It.IsAny<Action<AgentConfiguration>>()), Times.Never());
        _remoteConfigurationManager.Verify(rcm => rcm.RegisterProduct(It.IsAny<LiveDebuggerProduct>()), Times.Never());
        _debuggerSink.Verify(debuggerSink => debuggerSink.StartFlushingAsync(), Times.Never());
        _probeStatusPoller.Verify(poller => poller.StartPolling(), Times.Never());
    }
}
