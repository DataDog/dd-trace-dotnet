// <copyright file="TelemetryTransportFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;

namespace Datadog.Trace.Telemetry.Transports
{
    internal class TelemetryTransportFactory
    {
        public static ITelemetryTransport Create(
            TelemetrySettings telemetrySettings,
            ImmutableExporterSettings exporterSettings)
        {
            var requestFactory = telemetrySettings switch
            {
                { Agentless: { } a } => TelemetryTransportStrategy.GetDirectIntakeFactory(a.AgentlessUri, a.ApiKey),
                _ => TelemetryTransportStrategy.GetAgentIntakeFactory(exporterSettings),
            };

            return new JsonTelemetryTransport(requestFactory);
        }
    }
}
