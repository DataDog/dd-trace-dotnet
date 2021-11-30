// <copyright file="TelemetryDataBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryDataBuilder
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryDataBuilder>();
        private int _sequence = 0;

        public TelemetryData[] BuildTelemetryData(
            ApplicationTelemetryData application,
            HostTelemetryData host,
            ConfigTelemetryData configuration,
            ICollection<DependencyTelemetryData> dependencies,
            ICollection<IntegrationTelemetryData> integrations)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (configuration is not null)
            {
                Log.Debug("App initialized, sending app-started");
                var payload = new AppStartedPayload
                {
                    Configuration = configuration,
                    Dependencies = dependencies,
                    Integrations = integrations,
                };

                return new[] { GetRequest(application, host, TelemetryRequestTypes.AppStarted, payload) };
            }

            if (dependencies is null && integrations is null)
            {
                Log.Debug("No changes in telemetry");
                return Array.Empty<TelemetryData>();
            }

            if (dependencies is null)
            {
                Log.Debug("Integrations updated, sending app-integrations-change");
                var payload = new AppIntegrationsChangedPayload { Integrations = integrations };

                return new[] { GetRequest(application, host, TelemetryRequestTypes.AppIntegrationsChanged, payload), };
            }

            if (integrations is null)
            {
                Log.Debug("Dependencies updated, sending app-dependencies-loaded");
                var payload = new AppDependenciesLoadedPayload { Dependencies = dependencies };

                return new[] { GetRequest(application, host, TelemetryRequestTypes.AppDependenciesLoaded, payload), };
            }

            Log.Debug("Dependencies updated, sending app-dependencies-loaded");
            Log.Debug("Integrations updated, sending app-integrations-change");
            var depsPayload = new AppDependenciesLoadedPayload { Dependencies = dependencies };
            var integrationsPayload = new AppIntegrationsChangedPayload { Integrations = integrations };

            return new[]
            {
                GetRequest(application, host, TelemetryRequestTypes.AppDependenciesLoaded, depsPayload),
                GetRequest(application, host, TelemetryRequestTypes.AppIntegrationsChanged, integrationsPayload),
            };
        }

        public TelemetryData BuildHeartBeatTelemetryData(ApplicationTelemetryData application, HostTelemetryData host)
        {
            return GetRequest(application, host, TelemetryRequestTypes.AppHeartbeat, payload: null);
        }

        public TelemetryData BuildAppClosingTelemetryData(ApplicationTelemetryData application, HostTelemetryData host)
        {
            return GetRequest(application, host, TelemetryRequestTypes.AppClosing, payload: null);
        }

        private TelemetryData GetRequest(
            ApplicationTelemetryData application,
            HostTelemetryData host,
            string requestType,
            IPayload payload)
        {
            var sequence = Interlocked.Increment(ref _sequence);

            return new TelemetryData
            {
                SeqId = sequence,
                Application = application,
                Host = host,
                RuntimeId = Tracer.RuntimeId,
                TracerTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                RequestType = requestType,
                Payload = payload
            };
        }
    }
}
