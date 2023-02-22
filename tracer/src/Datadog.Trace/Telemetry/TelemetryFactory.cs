﻿// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using Datadog.Trace.Agent;
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
                        return NullTelemetryController.Instance;
                    }

                    var transportManager = new TelemetryTransportManager(telemetryTransports);

                    return new TelemetryController(
                        Configuration,
                        Dependencies,
                        Integrations,
                        transportManager,
                        TelemetryConstants.DefaultFlushInterval,
                        settings.HeartbeatInterval);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error initializing telemetry. Telemetry collection disabled.");
                }
            }

            return NullTelemetryController.Instance;
        }
    }
}
