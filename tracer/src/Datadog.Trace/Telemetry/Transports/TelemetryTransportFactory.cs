// <copyright file="TelemetryTransportFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Telemetry.Transports
{
    internal sealed class TelemetryTransportFactory
    {
        public TelemetryTransportFactory(TelemetrySettings telemetrySettings)
        {
            AgentTransportFactory = telemetrySettings switch
            {
                { AgentProxyEnabled: true } => e => GetAgentFactory(e, telemetrySettings),
                _ => null,
            };

            AgentlessTransport = telemetrySettings is { Agentless: { } a }
                                     ? GetAgentlessFactory(a, telemetrySettings)
                                     : null;
        }

        // Internal for testing
        internal TelemetryTransportFactory(
            Func<ExporterSettings, ITelemetryTransport>? agentTransportFactory,
            ITelemetryTransport? agentlessTransport)
        {
            AgentTransportFactory = agentTransportFactory;
            AgentlessTransport = agentlessTransport;
        }

        public Func<ExporterSettings, ITelemetryTransport>? AgentTransportFactory { get; }

        public ITelemetryTransport? AgentlessTransport { get; }

        public bool HasTransports => AgentTransportFactory is not null || AgentlessTransport is not null;

        private static ITelemetryTransport GetAgentFactory(ExporterSettings exporterSettings, TelemetrySettings telemetrySettings)
            => new AgentTelemetryTransport(
                TelemetryTransportStrategy.GetAgentIntakeFactory(exporterSettings),
                debugEnabled: telemetrySettings.DebugEnabled,
                telemetryCompressionMethod: telemetrySettings.CompressionMethod);

        private static ITelemetryTransport GetAgentlessFactory(TelemetrySettings.AgentlessSettings agentlessSettings, TelemetrySettings telemetrySettings)
            => new AgentlessTelemetryTransport(
                TelemetryTransportStrategy.GetDirectIntakeFactory(agentlessSettings),
                debugEnabled: telemetrySettings.DebugEnabled,
                telemetryCompressionMethod: telemetrySettings.CompressionMethod);
    }
}
