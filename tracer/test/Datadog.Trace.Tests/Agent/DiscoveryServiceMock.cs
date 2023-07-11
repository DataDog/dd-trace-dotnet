// <copyright file="DiscoveryServiceMock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;

namespace Datadog.Trace.Tests.Agent;

internal class DiscoveryServiceMock : IDiscoveryService
{
    public List<Action<AgentConfiguration>> Callbacks { get; } = new();

    public void TriggerChange(
        string configurationEndpoint = "configurationEndpoint",
        string debuggerEndpoint = "debuggerEndpoint",
        string symbolDbEndpoint = "symbolDbEndpoint",
        string agentVersion = "agentVersion",
        string statsEndpoint = "traceStatsEndpoint",
        string dataStreamsMonitoringEndpoint = "dataStreamsMonitoringEndpoint",
        string eventPlatformProxyEndpoint = "eventPlatformProxyEndpoint",
        string telemetryProxyEndpoint = "telemetryProxyEndpoint",
        bool clientDropP0 = true)
        => TriggerChange(
            new AgentConfiguration(
                configurationEndpoint: configurationEndpoint,
                debuggerEndpoint: debuggerEndpoint,
                symbolDbEndpoint: symbolDbEndpoint,
                agentVersion: agentVersion,
                statsEndpoint: statsEndpoint,
                dataStreamsMonitoringEndpoint: dataStreamsMonitoringEndpoint,
                eventPlatformProxyEndpoint: eventPlatformProxyEndpoint,
                telemetryProxyEndpoint: telemetryProxyEndpoint,
                clientDropP0: clientDropP0));

    public void TriggerChange(AgentConfiguration config)
    {
        foreach (var callback in Callbacks)
        {
            callback(config);
        }
    }

    public void SubscribeToChanges(Action<AgentConfiguration> callback)
    {
        Callbacks.Add(callback);
    }

    public void RemoveSubscription(Action<AgentConfiguration> callback)
    {
        Callbacks.Remove(callback);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
