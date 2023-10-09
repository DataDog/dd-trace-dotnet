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
        public static TelemetryTransports Create(TelemetrySettings telemetrySettings, ImmutableExporterSettings exporterSettings)
        {
            var agentProxy = telemetrySettings is { AgentProxyEnabled: true }
                                 ? GetAgentFactory(exporterSettings, telemetrySettings.DebugEnabled)
                                 : null;

            var agentless = telemetrySettings is { Agentless: { } a }
                                ? GetAgentlessFactory(a, telemetrySettings.DebugEnabled)
                                : null;

            return new TelemetryTransports(agentProxy, agentless);
        }

        private static ITelemetryTransport GetAgentFactory(ImmutableExporterSettings exporterSettings, bool debugEnabled)
            => new AgentTelemetryTransport(
                TelemetryTransportStrategy.GetAgentIntakeFactory(exporterSettings),
                debugEnabled: debugEnabled);

        private static ITelemetryTransport GetAgentlessFactory(TelemetrySettings.AgentlessSettings agentlessSettings, bool debugEnabled)
            => new AgentlessTelemetryTransport(
                TelemetryTransportStrategy.GetDirectIntakeFactory(agentlessSettings),
                debugEnabled: debugEnabled);
    }
}
