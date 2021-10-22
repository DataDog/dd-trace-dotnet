// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Tracer>();

        internal static ITelemetryController CreateTelemetryController()
            => CreateTelemetryController(TelemetrySettings.FromDefaultSources());

        internal static ITelemetryController CreateTelemetryController(TelemetrySettings settings)
        {
            if (settings.TelemetryEnabled)
            {
                try
                {
                    return new TelemetryController(
                        new ConfigurationTelemetryCollector(),
                        new DependencyTelemetryCollector(),
                        new IntegrationTelemetryCollector(),
                        new TelemetryTransportFactory(settings.TelemetryUrl).Create(),
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
