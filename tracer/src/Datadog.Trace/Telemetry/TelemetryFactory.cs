// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Tracer>();

        private static readonly ConfigurationTelemetryCollector Configuration = new();
        private static readonly DependencyTelemetryCollector Dependencies = new();
        private static readonly IntegrationTelemetryCollector Integrations = new();

        internal static ITelemetryController CreateTelemetryController(ImmutableTracerSettings tracerSettings)
        {
            var settings = TelemetrySettings.FromDefaultSources(tracerSettings);
            if (settings.TelemetryEnabled)
            {
                try
                {
                    return new TelemetryController(
                        Configuration,
                        Dependencies,
                        Integrations,
                        new TelemetryTransportFactory(settings.TelemetryUri, settings.ApiKey).Create(),
                        TelemetryConstants.RefreshInterval);
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
