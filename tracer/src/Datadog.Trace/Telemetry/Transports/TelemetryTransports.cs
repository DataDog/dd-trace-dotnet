// <copyright file="TelemetryTransports.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Telemetry.Transports;

internal class TelemetryTransports
{
    public TelemetryTransports(ITelemetryTransport? agentTransport, ITelemetryTransport? agentlessTransport)
    {
        AgentTransport = agentTransport;
        AgentlessTransport = agentlessTransport;
    }

    public ITelemetryTransport? AgentTransport { get; }

    public ITelemetryTransport? AgentlessTransport { get; }

    public bool HasTransports => AgentTransport is not null || AgentlessTransport is not null;
}
