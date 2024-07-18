// <copyright file="AgentConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Agent.DiscoveryService;

internal record AgentConfiguration
{
    public AgentConfiguration(
        string? configurationEndpoint,
        string? debuggerEndpoint,
        string? symbolDbEndpoint,
        string? agentVersion,
        string? statsEndpoint,
        string? dataStreamsMonitoringEndpoint,
        string? eventPlatformProxyEndpoint,
        string? telemetryProxyEndpoint,
        string? tracerFlareEndpoint,
        bool clientDropP0,
        string? diagnosticsEndpoint,
        bool spanMetaStructs)
    {
        ConfigurationEndpoint = configurationEndpoint;
        DebuggerEndpoint = debuggerEndpoint;
        DiagnosticsEndpoint = diagnosticsEndpoint;
        SymbolDbEndpoint = symbolDbEndpoint;
        AgentVersion = agentVersion;
        StatsEndpoint = statsEndpoint;
        DataStreamsMonitoringEndpoint = dataStreamsMonitoringEndpoint;
        EventPlatformProxyEndpoint = eventPlatformProxyEndpoint;
        TelemetryProxyEndpoint = telemetryProxyEndpoint;
        TracerFlareEndpoint = tracerFlareEndpoint;
        ClientDropP0s = clientDropP0;
        SpanMetaStructs = spanMetaStructs;
    }

    public string? ConfigurationEndpoint { get; }

    public string? DebuggerEndpoint { get; }

    public string? DiagnosticsEndpoint { get; }

    public string? SymbolDbEndpoint { get; }

    public string? AgentVersion { get; }

    public string? StatsEndpoint { get; }

    public string? DataStreamsMonitoringEndpoint { get; }

    public string? EventPlatformProxyEndpoint { get; }

    public string? TelemetryProxyEndpoint { get; }

    public string? TracerFlareEndpoint { get; }

    public bool ClientDropP0s { get; }

    public bool SpanMetaStructs { get; }
}
