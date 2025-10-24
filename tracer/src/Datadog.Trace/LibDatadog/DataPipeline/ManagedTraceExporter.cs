// <copyright file="ManagedTraceExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.LibDatadog.DataPipeline;

/// <summary>
/// A "managed" version of <see cref="TraceExporter"/> that responds to changes in settings by replacing the trace exporter
/// </summary>
internal class ManagedTraceExporter : IApi
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ManagedTraceExporter>();
    private TraceExporter _current;

    private ManagedTraceExporter(
        TraceExporter traceExporter,
        TracerSettings settings,
        Action<Dictionary<string, float>> updateSampleRates,
        TelemetrySettings telemetrySettings)
    {
        _current = traceExporter;
        settings.Manager.SubscribeToChanges(changes =>
        {
            // avoid changes if we don't need them
            // only care about exporter and service/version/env
            if (changes.UpdatedExporter is not null
             || (changes.UpdatedMutable is { } mutable
              && (!string.Equals(mutable.DefaultServiceName, changes.PreviousMutable.DefaultServiceName)
               || !string.Equals(mutable.Environment, changes.PreviousMutable.Environment)
               || !string.Equals(mutable.ServiceVersion, changes.PreviousMutable.ServiceVersion))))
            {
                var exporter = CreateTraceExporter(
                    settings,
                    changes.UpdatedMutable ?? changes.PreviousMutable,
                    changes.UpdatedExporter ?? changes.PreviousExporter,
                    updateSampleRates,
                    telemetrySettings);

                var previous = Interlocked.Exchange(ref _current, exporter);
                if (previous is not null)
                {
                    previous.Dispose();
                }
            }
        });
    }

    public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool apmTracingEnabled = true)
        => Volatile.Read(ref _current).SendTracesAsync(traces, numberOfTraces, statsComputationEnabled, numberOfDroppedP0Traces, numberOfDroppedP0Spans, apmTracingEnabled);

    public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
        => Volatile.Read(ref _current).SendStatsAsync(stats, bucketDuration);

    // Internal for testing
    internal static bool TryCreateTraceExporter(
        TracerSettings settings,
        Action<Dictionary<string, float>> updateSampleRates,
        TelemetrySettings telemetrySettings,
        [NotNullWhen(true)]out ManagedTraceExporter? traceExporter)
    {
        try
        {
            // We try to create the "initial" version up front, because if it _doesn't_ work, then
            // we assume
            var initialExporter = CreateTraceExporter(
                settings,
                settings.Manager.InitialMutableSettings,
                settings.Manager.InitialExporterSettings,
                updateSampleRates,
                telemetrySettings);
            traceExporter = new ManagedTraceExporter(initialExporter, settings, updateSampleRates, telemetrySettings);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create native Trace Exporter, falling back to managed API");
            traceExporter = null;
            return false;
        }
    }

    private static TraceExporter CreateTraceExporter(
        TracerSettings settings,
        MutableSettings mutableSettings,
        ExporterSettings exporterSettings,
        Action<Dictionary<string, float>> updateSampleRates,
        TelemetrySettings telemetrySettings)
    {
        // If file logging is enabled, then enable logging in libdatadog
        // We assume that we can't go from pipeline enabled -> disabled, so we should never need to call logger.Disable()
        // Note that this _could_ fail if there's an issue in libdatadog, but we continue to _Try_ to initialize the exporter anyway
        // If this was previously initialized, it will be re-initialized with the new settings, which is fine
        if (Log.FileLoggingConfiguration is { } fileConfig)
        {
            var logger = LibDatadog.Logging.Logger.Instance;
            logger.Enable(fileConfig, DomainMetadata.Instance);

            // hacky to use the global setting, but about the only option we have atm
            logger.SetLogLevel(GlobalSettings.Instance.DebugEnabledInternal);
        }

        TelemetryClientConfiguration? telemetryClientConfiguration = null;

        // We don't know how to handle telemetry in Agentless mode yet
        // so we disable telemetry in this case
        if (telemetrySettings.TelemetryEnabled && telemetrySettings.Agentless == null)
        {
            telemetryClientConfiguration = new TelemetryClientConfiguration
            {
                Interval = (ulong)telemetrySettings.HeartbeatInterval.TotalMilliseconds,
                RuntimeId = new CharSlice(Tracer.RuntimeId),
                DebugEnabled = telemetrySettings.DebugEnabled
            };
        }

        // When APM is disabled, we don't want to compute stats at all
        // A common use case is in Application Security Monitoring (ASM) scenarios:
        // when APM is disabled but ASM is enabled.
        var clientComputedStats = !settings.StatsComputationEnabled && !settings.ApmTracingEnabled;

        var frameworkDescription = FrameworkDescription.Instance;
        using var configuration = new TraceExporterConfiguration
        {
            Url = GetUrl(exporterSettings),
            TraceVersion = TracerConstants.AssemblyVersion,
            Env = mutableSettings.Environment,
            Version = mutableSettings.ServiceVersion,
            Service = mutableSettings.DefaultServiceName,
            Hostname = HostMetadata.Instance.Hostname,
            Language = TracerConstants.Language,
            LanguageVersion = frameworkDescription.ProductVersion,
            LanguageInterpreter = frameworkDescription.Name,
            ComputeStats = settings.StatsComputationEnabled,
            TelemetryClientConfiguration = telemetryClientConfiguration,
            ClientComputedStats = clientComputedStats,
            ConnectionTimeoutMs = 15_000
        };

        return new TraceExporter(configuration, updateSampleRates);

        static string GetUrl(ExporterSettings exporterSettings) =>
            exporterSettings.TracesTransport switch
            {
                TracesTransportType.WindowsNamedPipe => $"windows://./pipe/{exporterSettings.TracesPipeName}",
                TracesTransportType.UnixDomainSocket => $"unix://{exporterSettings.TracesUnixDomainSocketPath}",
                _ => exporterSettings.AgentUri.ToString()
            };
    }
}
