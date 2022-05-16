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
                { AgentProxyEnabled: true, Agentless: { } a } => new[] { GetAgentFactory(exporterSettings), GetAgentlessFactory(a) },
                { AgentProxyEnabled: true } => new[] { GetAgentFactory(exporterSettings) },
                { Agentless: { } a } => new[] { GetAgentlessFactory(a) },
                _ => Array.Empty<ITelemetryTransport>(),
            };
        }

        private static ITelemetryTransport GetAgentFactory(ImmutableExporterSettings exporterSettings)
            => new AgentTelemetryTransport(
                TelemetryTransportStrategy.GetAgentIntakeFactory(exporterSettings));

        private static ITelemetryTransport GetAgentlessFactory(TelemetrySettings.AgentlessSettings agentlessSettings)
            => new AgentlessTelemetryTransport(
                TelemetryTransportStrategy.GetDirectIntakeFactory(agentlessSettings.AgentlessUri, agentlessSettings.ApiKey));
    }
}
