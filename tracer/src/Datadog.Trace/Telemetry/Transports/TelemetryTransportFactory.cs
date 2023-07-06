// <copyright file="TelemetryTransportFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Telemetry.Transports
{
    internal class TelemetryTransportFactory
    {
        public static ITelemetryTransport[] Create(TelemetrySettings telemetrySettings, ImmutableExporterSettings exporterSettings)
        {
            return telemetrySettings switch
            {
                // order of transports here controls the order they will be used
                // so we default to using the agent first, and then agentless
                { AgentProxyEnabled: true, Agentless: { } a } => new[] { GetAgentFactory(exporterSettings, telemetrySettings.DebugEnabled), GetAgentlessFactory(a, telemetrySettings.DebugEnabled) },
                { AgentProxyEnabled: true } => new[] { GetAgentFactory(exporterSettings, telemetrySettings.DebugEnabled) },
                { Agentless: { } a } => new[] { GetAgentlessFactory(a, telemetrySettings.DebugEnabled) },
                _ => Array.Empty<ITelemetryTransport>(),
            };
        }

        private static ITelemetryTransport GetAgentFactory(ImmutableExporterSettings exporterSettings, bool debugEnabled)
            => new AgentTelemetryTransport(
                TelemetryTransportStrategy.GetAgentIntakeFactory(exporterSettings),
                debugEnabled: debugEnabled);

        private static ITelemetryTransport GetAgentlessFactory(TelemetrySettings.AgentlessSettings agentlessSettings, bool debugEnabled)
            => new AgentlessTelemetryTransport(
                TelemetryTransportStrategy.GetDirectIntakeFactory(agentlessSettings.AgentlessUri, agentlessSettings.ApiKey),
                debugEnabled: debugEnabled);
    }
}
