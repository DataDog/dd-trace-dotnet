// <copyright file="TelemetryDataBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry;

internal class TelemetryDataBuilder
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryDataBuilder>();
    private int _sequence = 0;

    public TelemetryData BuildTelemetryData(
        ApplicationTelemetryData application,
        HostTelemetryData host,
        in TelemetryInput input,
        string? namingSchemeVersion,
        bool sendAppClosing)
    {
        List<MessageBatchData>? data = null;

        if (input.SendAppStarted)
        {
            Log.Debug("App started, sending app-started");
            data = new()
            {
                new(TelemetryRequestTypes.AppStarted, new AppStartedPayload()
                {
                    Configuration = input.Configuration,
                    Products = input.Products,
                    InstallSignature = GetInstallSignature()
                })
            };
        }
        else
        {
            if (input.Configuration is { } configuration)
            {
                Log.Debug("Configuration updated, sending app-client-configuration-change");
                data = new()
                {
                    new(
                        TelemetryRequestTypes.AppClientConfigurationChanged,
                        new AppClientConfigurationChangedPayload(configuration))
                };
            }

            if (input.Products is { } products)
            {
                Log.Debug("Products updated, sending app-product-change");
                data ??= new();
                data.Add(new(
                    TelemetryRequestTypes.AppProductChanged,
                    new AppProductChangePayload(products)));
            }
        }

        if (input.Dependencies is { } dependencies)
        {
            var skip = 0;
            Log.Debug<int>("Dependencies updated, sending app-dependencies-loaded with {Count} dependencies", dependencies.Count);
            while (skip < dependencies.Count)
            {
                const int maxPerMessage = 2000;
                // Only 2000 dependencies should be included per message, so need to split into separate requests
                var depsToSend = dependencies.Count > maxPerMessage
                                     ? dependencies.Skip(skip).Take(maxPerMessage).ToList()
                                     : dependencies;
                skip += depsToSend.Count;

                data ??= new();
                data.Add(new(
                             TelemetryRequestTypes.AppDependenciesLoaded,
                             new AppDependenciesLoadedPayload(depsToSend)));
            }
        }

        if (input.Integrations is { } integrations)
        {
            Log.Debug("Integrations updated, sending app-integrations-change");
            data ??= new();
            data.Add(new(
                TelemetryRequestTypes.AppIntegrationsChanged,
                new AppIntegrationsChangedPayload(integrations)));
        }

        if (input.Metrics is { } metrics)
        {
            Log.Debug("Metrics updated, sending generate-metrics");
            data ??= new();
            data.Add(new(
                TelemetryRequestTypes.GenerateMetrics,
                new GenerateMetricsPayload(metrics)));
        }

        if (input.Distributions is { } distributions)
        {
            Log.Debug("Distributions updated, sending distributions");
            data ??= new();
            data.Add(new(
                TelemetryRequestTypes.Distributions,
                new DistributionsPayload(distributions)));
        }

        if (sendAppClosing)
        {
            Log.Debug("Final push, sending app-closing");
            if (data is null)
            {
                return GetRequest(application, host, TelemetryRequestTypes.AppClosing, payload: null, namingSchemeVersion);
            }

            data.Add(new(TelemetryRequestTypes.AppClosing, payload: null));
        }
        else
        {
            if (data is null)
            {
                Log.Debug("No changes in telemetry, sending app-heartbeat");
                return GetRequest(application, host, TelemetryRequestTypes.AppHeartbeat, payload: null, namingSchemeVersion);
            }

            if (!input.SendAppStarted)
            {
                // don't include the app heartbeat in the app-started request batch
                data.Add(new(TelemetryRequestTypes.AppHeartbeat, payload: null));
            }
        }

        return GetRequest(application, host, new MessageBatchPayload(data), namingSchemeVersion);
    }

    public TelemetryData BuildLogsTelemetryData(ApplicationTelemetryData application, HostTelemetryData host, List<LogMessageData> logs, string? namingSchemeVersion)
                => GetRequest(application, host, TelemetryRequestTypes.RedactedErrorLogs, new LogsPayload(logs), namingSchemeVersion);

    public TelemetryData BuildHeartbeatData(ApplicationTelemetryData application, HostTelemetryData host, string? namingSchemeVersion)
        => GetRequest(application, host, TelemetryRequestTypes.AppHeartbeat, payload: null, namingSchemeVersion);

    public TelemetryData BuildExtendedHeartbeatData(
        ApplicationTelemetryData application,
        HostTelemetryData host,
        ICollection<ConfigurationKeyValue>? configuration,
        ICollection<DependencyTelemetryData>? dependencies,
        ICollection<IntegrationTelemetryData>? integrations,
        string? namingSchemeVersion)
        => GetRequest(
            application,
            host,
            TelemetryRequestTypes.AppExtendedHeartbeat,
            payload: new AppExtendedHeartbeatPayload
            {
                Configuration = configuration,
                Dependencies = dependencies,
                Integrations = integrations
            },
            namingSchemeVersion);

    private static AppStartedPayload.InstallSignaturePayload? GetInstallSignature()
    {
        var installId = EnvironmentHelpers.GetEnvironmentVariable("DD_INSTRUMENTATION_INSTALL_ID");
        var installType = EnvironmentHelpers.GetEnvironmentVariable("DD_INSTRUMENTATION_INSTALL_TYPE");
        var installTime = EnvironmentHelpers.GetEnvironmentVariable("DD_INSTRUMENTATION_INSTALL_TIME");

        if (string.IsNullOrEmpty(installId) && string.IsNullOrEmpty(installType) && string.IsNullOrEmpty(installTime))
        {
            return null;
        }

        return new AppStartedPayload.InstallSignaturePayload
        {
            InstallId = installId,
            InstallType = installType,
            InstallTime = installTime
        };
    }

    private TelemetryData GetRequest(
        ApplicationTelemetryData application,
        HostTelemetryData host,
        string requestType,
        IPayload? payload,
        string? namingSchemeVersion)
    {
        var sequence = Interlocked.Increment(ref _sequence);

        return new TelemetryData(
            requestType: requestType,
            tracerTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            runtimeId: Tracer.RuntimeId,
            seqId: sequence,
            application: application,
            host: host,
            payload: payload)
        {
            NamingSchemaVersion = namingSchemeVersion,
        };
    }

    private TelemetryData GetRequest(
        ApplicationTelemetryData application,
        HostTelemetryData host,
        MessageBatchPayload? payload,
        string? namingSchemeVersion)
    {
        var sequence = Interlocked.Increment(ref _sequence);

        return new TelemetryData(
            requestType: TelemetryRequestTypes.MessageBatch,
            tracerTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            runtimeId: Tracer.RuntimeId,
            seqId: sequence,
            application: application,
            host: host,
            payload: payload)
        {
            NamingSchemaVersion = namingSchemeVersion,
        };
    }
}
