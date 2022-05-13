// <copyright file="TelemetryTransportFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Telemetry.Transports
{
    internal class TelemetryTransportFactory
    {
        public static ITelemetryTransport[] Create(TelemetrySettings telemetrySettings, ImmutableExporterSettings exporterSettings)
        {
            var agentTransport = new AgentTelemetryTransport(TelemetryTransportStrategy.GetAgentIntakeFactory(exporterSettings));

            if (telemetrySettings.Agentless is { } a)
            {
                // order of transports here controls the order they will be used
                // so we default to using the agent first, and then agentless
                return new ITelemetryTransport[]
                {
                    agentTransport,
                    new AgentlessTelemetryTransport(TelemetryTransportStrategy.GetDirectIntakeFactory(a.AgentlessUri, a.ApiKey))
                };
            }
            else
            {
                return new ITelemetryTransport[] { agentTransport };
            }
        }
    }
}
