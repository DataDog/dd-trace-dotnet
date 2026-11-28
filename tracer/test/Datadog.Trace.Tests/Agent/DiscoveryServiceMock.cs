// <copyright file="DiscoveryServiceMock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Tests.Agent;

internal class DiscoveryServiceMock : IDiscoveryService
{
    private readonly object _publicationGate = new();
    private AgentConfiguration _configuration;

    public List<Action<AgentConfiguration>> Callbacks { get; } = new();

    public AgentConfiguration CurrentConfiguration => Volatile.Read(ref _configuration);

    public bool TryRunWithCurrentConfiguration(Func<AgentConfiguration, bool> apply)
    {
        lock (_publicationGate)
        {
            return _configuration is { } configuration && apply(configuration);
        }
    }

    public void TriggerChange(
        string configurationEndpoint = "configurationEndpoint",
        string debuggerEndpoint = "debuggerEndpoint",
        string debuggerV2Endpoint = "debuggerV2Endpoint",
        string diagnosticsEndpoint = "diagnosticsEndpoint",
        string symbolDbEndpoint = "symbolDbEndpoint",
        string agentVersion = "agentVersion",
        string statsEndpoint = "traceStatsEndpoint",
        string dataStreamsMonitoringEndpoint = "dataStreamsMonitoringEndpoint",
        string eventPlatformProxyEndpoint = "eventPlatformProxyEndpoint",
        string telemetryProxyEndpoint = "telemetryProxyEndpoint",
        string tracerFlareEndpoint = "tracerFlareEndpoint",
        string containerTagsHash = "containerTagsHash",
        bool clientDropP0 = true,
        bool spanMetaStructs = true,
        bool spanEvents = true,
        bool eventPlatformProxySupportsEvpOriginHeaders = true,
        ExporterSettings discoverySettings = null)
        => TriggerChange(
            new AgentConfiguration(
                configurationEndpoint: configurationEndpoint,
                debuggerEndpoint: debuggerEndpoint,
                debuggerV2Endpoint: debuggerV2Endpoint,
                diagnosticsEndpoint: diagnosticsEndpoint,
                symbolDbEndpoint: symbolDbEndpoint,
                agentVersion: agentVersion,
                statsEndpoint: statsEndpoint,
                dataStreamsMonitoringEndpoint: dataStreamsMonitoringEndpoint,
                eventPlatformProxyEndpoint: eventPlatformProxyEndpoint,
                telemetryProxyEndpoint: telemetryProxyEndpoint,
                tracerFlareEndpoint: tracerFlareEndpoint,
                containerTagsHash: containerTagsHash,
                clientDropP0: clientDropP0,
                spanMetaStructs: spanMetaStructs,
                spanEvents: spanEvents,
                eventPlatformProxySupportsEvpOriginHeaders: eventPlatformProxySupportsEvpOriginHeaders,
                discoverySettings: discoverySettings));

    public void TriggerChange(AgentConfiguration config)
    {
        Action<AgentConfiguration>[] callbacks;
        lock (_publicationGate)
        {
            Volatile.Write(ref _configuration, config);
            callbacks = Callbacks.ToArray();
        }

        foreach (var callback in callbacks)
        {
            callback(config);
        }
    }

    public void SubscribeToChanges(Action<AgentConfiguration> callback)
    {
        lock (_publicationGate)
        {
            Callbacks.Add(callback);
        }
    }

    public void RemoveSubscription(Action<AgentConfiguration> callback)
    {
        lock (_publicationGate)
        {
            Callbacks.Remove(callback);
        }
    }

    public void SetCurrentConfigStateHash(string configStateHash)
    {
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
