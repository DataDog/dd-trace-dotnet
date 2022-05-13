// <copyright file="AgentTelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Agent;

namespace Datadog.Trace.Telemetry.Transports;

internal class AgentTelemetryTransport : JsonTelemetryTransport
{
    private string? _agentVersion;

    public AgentTelemetryTransport(IApiRequestFactory requestFactory)
        : base(requestFactory)
    {
    }

    /// <summary>
    /// Gets detected agent version. Internal for testing only
    /// </summary>
    internal string? DetectedAgentVersion => _agentVersion;

    protected override TelemetryPushResult? HandleErrorResponse(IApiResponse response)
    {
        if (_agentVersion is null)
        {
            var agentVersion = response.GetHeader(AgentHttpHeaderNames.AgentVersion) ?? string.Empty;
            if (agentVersion != _agentVersion)
            {
                _agentVersion = agentVersion;
                if (!Version.TryParse(agentVersion, out var parsedVersion) || parsedVersion < new Version(7, 34, 0))
                {
                    var detectedVersion = string.IsNullOrEmpty(agentVersion) ? "{detection failed}" : agentVersion;
                    Log.Debug("Error sending telemetry. Telemetry via the agent can only be enabled with agent 7.34.0+ (detected version: {version})", detectedVersion);
                    return TelemetryPushResult.FatalErrorDontRetry;
                }
            }
        }

        return null;
    }
}
