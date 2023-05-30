// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Telemetry.Transports;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryFactory>();

        private static readonly ConfigurationTelemetryCollector Configuration = new();
        private static readonly IntegrationTelemetryCollector Integrations = new();

        internal static ITelemetryController CreateTelemetryController(ImmutableTracerSettings tracerSettings)
        {
            var settings = TelemetrySettings.FromSource(GlobalConfigurationSource.Instance, TelemetryFactoryV2.GetConfigTelemetry());
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

                    IDependencyTelemetryCollector dependencies = settings.DependencyCollectionEnabled
                                           ? DependencyTelemetryCollector.Instance
                                           : NullDependencyTelemetryCollector.Instance;

                    return new TelemetryController(
                        Configuration,
                        dependencies,
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
