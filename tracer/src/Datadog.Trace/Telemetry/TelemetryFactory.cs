// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Transports;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Datadog.Trace.Telemetry.TelemetryFactory>();

        private static readonly ConfigurationTelemetryCollector Configuration = new();
        private static readonly DependencyTelemetryCollector Dependencies = new();
        private static readonly IntegrationTelemetryCollector Integrations = new();

        internal static ITelemetryController CreateTelemetryController(ImmutableTracerSettings tracerSettings)
        {
            var settings = TelemetrySettings.FromDefaultSources();
            if (settings.TelemetryEnabled)
            {
                try
                {
                    var telemetryTransports = TelemetryTransportFactory.Create(settings, tracerSettings.Exporter);

                    if (telemetryTransports.Length == 0)
                    {
                        // telemetry is not enabled, so disable the telemetry sink
                        // I don't like that this modifies the static instance, but what's the other options?
                        DatadogLogging.GetGlobalTelemetrySink()?.CloseImmediately();
                        return NullTelemetryController.Instance;
                    }

                    return new TelemetryController(
                        Configuration,
                        Dependencies,
                        Integrations,
                        telemetryTransports,
                        TelemetryDataBuilder.Instance, // must use the shared instance so we get the sequence number correct
                        TelemetryConstants.DefaultFlushInterval,
                        settings.HeartbeatInterval,
                        DatadogLogging.GetGlobalTelemetrySink());
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error initializing telemetry. Telemetry collection disabled.");
                }
            }

            // telemetry is not enabled, so disable the telemetry sink
            // I don't like that this modifies the static instance, but what's the other options?
            DatadogLogging.GetGlobalTelemetrySink()?.CloseImmediately();
            return NullTelemetryController.Instance;
        }
    }
}
