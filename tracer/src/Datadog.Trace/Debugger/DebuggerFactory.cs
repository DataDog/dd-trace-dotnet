// <copyright file="DebuggerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.StatsdClient;
using Datadog.Trace.Vendors.StatsdClient.Transport;

namespace Datadog.Trace.Debugger;

internal sealed class DebuggerFactory
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerFactory));

    internal static DynamicInstrumentation CreateDynamicInstrumentation(IDiscoveryService discoveryService, IRcmSubscriptionManager remoteConfigurationManager, TracerSettings tracerSettings, Func<string> serviceNameProvider, DebuggerSettings debuggerSettings, IGitMetadataTagsProvider gitMetadataTagsProvider)
    {
        var globalRateLimiter = DebuggerGlobalRateLimiter.Instance;
        var snapshotSlicer = SnapshotSlicer.Create(debuggerSettings);
        var snapshotSink = SnapshotSink.Create(debuggerSettings, snapshotSlicer, MetricTags.DebuggerCaptureEventType.Snapshot);
        var logSink = SnapshotSink.Create(debuggerSettings, snapshotSlicer, MetricTags.DebuggerCaptureEventType.Log);
        var diagnosticsSink = DiagnosticsSink.Create(serviceNameProvider, debuggerSettings);

        var settings = tracerSettings.Manager;
        var uploadTransport = new DebuggerAgentTransport(settings, discoveryService, CreateAgentRequestFactory);
        try
        {
            var snapshotUploader = SnapshotUploader.Create(snapshotSink, BatchUploader.Create(new SnapshotUploadApi(uploadTransport, gitMetadataTagsProvider)), debuggerSettings);
            var logUploader = SnapshotUploader.Create(logSink, BatchUploader.Create(new LogUploadApi(uploadTransport, gitMetadataTagsProvider)), debuggerSettings);
            var diagnosticsUploader = DiagnosticsUploader.Create(diagnosticsSink, BatchUploader.Create(new DiagnosticsUploadApi(uploadTransport, gitMetadataTagsProvider)), debuggerSettings);
            var lineProbeResolver = LineProbeResolver.Create(debuggerSettings.ThirdPartyDetectionExcludes, debuggerSettings.ThirdPartyDetectionIncludes);
            var probeStatusPoller = ProbeStatusPoller.Create(diagnosticsSink, debuggerSettings);
            var configurationUpdater = ConfigurationUpdater.Create(
                () =>
                {
                    var target = settings.CurrentMutableSettings;
                    return new(target.Environment, target.ServiceVersion);
                },
                debuggerSettings.MaxProbesPerType,
                globalRateLimiter);
            var memoryPressureMonitor = new MemoryPressureMonitor(MemoryPressureConfig.Default);

            var statsd = GetDogStatsd(tracerSettings);

            return new DynamicInstrumentation(
                settings: debuggerSettings,
                discoveryService: discoveryService,
                remoteConfigurationManager: remoteConfigurationManager,
                lineProbeResolver: lineProbeResolver,
                snapshotUploader: snapshotUploader,
                logUploader: logUploader,
                diagnosticsUploader: diagnosticsUploader,
                probeStatusPoller: probeStatusPoller,
                configurationUpdater: configurationUpdater,
                dogStats: statsd,
                globalRateLimiter: globalRateLimiter,
                memoryPressureMonitor: memoryPressureMonitor,
                uploadTransport: uploadTransport,
                ownerSettings: settings);
        }
        catch
        {
            uploadTransport.Dispose();
            throw;
        }
    }

    private static IDogStatsd GetDogStatsd(TracerSettings tracerSettings)
    {
        IDogStatsd statsd;
        if (FrameworkDescription.Instance.IsWindows()
         && tracerSettings.Manager.InitialExporterSettings.MetricsTransport == TransportType.UDS)
        {
            Log.Information("Metric probes are not supported on Windows when transport type is UDS");
            statsd = NoOpStatsd.Instance;
        }
        else
        {
            // TODO: use StatsdManager to get automatic updating on exporter and other setting changes
            statsd = StatsdFactory.CreateDogStatsdClient(
                tracerSettings.Manager.InitialMutableSettings,
                tracerSettings.Manager.InitialExporterSettings,
                includeDefaultTags: false,
                prefix: DebuggerSettings.DebuggerMetricPrefix);
        }

        return statsd;
    }

    internal static IDebuggerUploader CreateSymbolsUploader(IDiscoveryService discoveryService, Func<string> serviceNameProvider, TracerSettings tracerSettings, DebuggerSettings settings, IGitMetadataTagsProvider gitMetadataTagsProvider)
    {
        var symbolBatchApi = DebuggerUploadApiFactory.CreateSymbolsUploadApi(CreateAgentRequestFactory(tracerSettings.Manager.InitialExporterSettings), discoveryService, gitMetadataTagsProvider, settings.SymbolDatabaseCompressionEnabled);
        var symbolsUploader = SymbolsUploader.Create(symbolBatchApi, discoveryService, tracerSettings, settings, serviceNameProvider);
        return symbolsUploader;
    }

    private static IApiRequestFactory CreateAgentRequestFactory(ExporterSettings exporter)
    {
        return AgentTransportStrategy.Get(
            exporter,
            productName: "debugger",
            tcpTimeout: TimeSpan.FromSeconds(15),
            httpHeaderHelper: MinimalAgentHeaderHelper.Instance);
    }
}
