// <copyright file="TelemetryDataBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryDataBuilder
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryDataBuilder>();
        private int _sequence = 0;

        public TelemetryData[] BuildTelemetryData(
            ApplicationTelemetryData? application,
            HostTelemetryData? host,
            ConfigTelemetryData? configuration,
            ICollection<DependencyTelemetryData>? dependencies,
            ICollection<IntegrationTelemetryData>? integrations,
            bool sendHeartbeat)
        {
            if (application is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(application));
            }

            if (host is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(host));
            }

            if (configuration is not null)
            {
                Log.Debug("App initialized, sending app-started");
                var payload = new AppStartedPayload(
                    configuration: configuration,
                    dependencies: dependencies,
                    integrations: integrations);

                return new[] { GetRequest(application, host, TelemetryRequestTypes.AppStarted, payload) };
            }

            if (dependencies is not null && integrations is not null)
            {
                Log.Debug("Dependencies updated, sending app-dependencies-loaded");
                Log.Debug("Integrations updated, sending app-integrations-change");
                var depsPayload = new AppDependenciesLoadedPayload(dependencies: dependencies);
                var integrationsPayload = new AppIntegrationsChangedPayload(integrations: integrations);

                return new[]
                {
                    GetRequest(application, host, TelemetryRequestTypes.AppDependenciesLoaded, depsPayload),
                    GetRequest(application, host, TelemetryRequestTypes.AppIntegrationsChanged, integrationsPayload),
                };
            }

            if (integrations is not null)
            {
                Log.Debug("Integrations updated, sending app-integrations-change");
                var payload = new AppIntegrationsChangedPayload(integrations: integrations);

                return new[] { GetRequest(application, host, TelemetryRequestTypes.AppIntegrationsChanged, payload), };
            }

            if (dependencies is not null)
            {
                Log.Debug("Dependencies updated, sending app-dependencies-loaded");
                var payload = new AppDependenciesLoadedPayload(dependencies: dependencies);

                return new[] { GetRequest(application, host, TelemetryRequestTypes.AppDependenciesLoaded, payload), };
            }

            if (sendHeartbeat)
            {
                Log.Debug("No changes in telemetry, sending heartbeat");
                return new[] { GetRequest(application, host, TelemetryRequestTypes.AppHeartbeat, payload: null) };
            }

            Log.Debug("No changes in telemetry");
            return Array.Empty<TelemetryData>();
        }

        public TelemetryData BuildAppClosingTelemetryData(ApplicationTelemetryData? application, HostTelemetryData? host)
        {
            if (application is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(application));
            }

            if (host is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(host));
            }

            return GetRequest(application, host, TelemetryRequestTypes.AppClosing, payload: null);
        }

        private TelemetryData GetRequest(
            ApplicationTelemetryData application,
            HostTelemetryData host,
            string requestType,
            IPayload? payload)
        {
            var sequence = Interlocked.Increment(ref _sequence);

            return new TelemetryData(
                requestType: requestType,
                tracerTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                runtimeId: Tracer.RuntimeId,
                seqId: sequence,
                application: application,
                host: host,
                payload: payload);
        }
    }
}
