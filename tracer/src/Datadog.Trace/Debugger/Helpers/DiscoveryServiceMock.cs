// <copyright file="DiscoveryServiceMock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;

namespace Datadog.Trace.Debugger.Helpers
{
    internal class DiscoveryServiceMock : IDiscoveryService
    {
        internal bool Called { get; private set; }

        public void SubscribeToChanges(Action<AgentConfiguration> callback)
        {
            Called = true;
            callback(
                new AgentConfiguration(
                    configurationEndpoint: "configurationEndpoint",
                    debuggerEndpoint: "debuggerEndpoint",
                    diagnosticsEndpoint: "diagnosticsEndpoint",
                    symbolDbEndpoint: "symbolDbEndpoint",
                    agentVersion: "agentVersion",
                    statsEndpoint: "traceStatsEndpoint",
                    dataStreamsMonitoringEndpoint: "dataStreamsMonitoringEndpoint",
                    eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
                    telemetryProxyEndpoint: "telemetryProxyEndpoint",
                    tracerFlareEndpoint: "tracerFlareEndpoint",
                    clientDropP0: false,
                    spanMetaStructs: true));
        }

        public void RemoveSubscription(Action<AgentConfiguration> callback)
        {
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
