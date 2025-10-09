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
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.StatsdClient;
using Datadog.Trace.Vendors.StatsdClient.Transport;

namespace Datadog.Trace.Debugger;

internal class DebuggerFactory
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerFactory));

    internal static DynamicInstrumentation CreateDynamicInstrumentation(IDiscoveryService discoveryService, IRcmSubscriptionManager remoteConfigurationManager, TracerSettings tracerSettings, string serviceName, DebuggerSettings debuggerSettings, IGitMetadataTagsProvider gitMetadataTagsProvider)
    {
        var snapshotSlicer = SnapshotSlicer.Create(debuggerSettings);
        var snapshotStatusSink = SnapshotSink.Create(debuggerSettings, snapshotSlicer);
        var diagnosticsSink = DiagnosticsSink.Create(serviceName, debuggerSettings);

        var debuggerUploader = CreateSnapshotUploader(discoveryService, debuggerSettings, gitMetadataTagsProvider, GetApiFactory(tracerSettings, false), snapshotStatusSink);
        var diagnosticsUploader = CreateDiagnosticsUploader(discoveryService, debuggerSettings, gitMetadataTagsProvider, GetApiFactory(tracerSettings, true), diagnosticsSink);
        var lineProbeResolver = LineProbeResolver.Create(debuggerSettings.ThirdPartyDetectionExcludes, debuggerSettings.ThirdPartyDetectionIncludes);
        var probeStatusPoller = ProbeStatusPoller.Create(diagnosticsSink, debuggerSettings);
        var configurationUpdater = ConfigurationUpdater.Create(tracerSettings.Environment, tracerSettings.ServiceVersion);

        var statsd = GetDogStatsd(tracerSettings, serviceName);

        return new DynamicInstrumentation(
            settings: debuggerSettings,
            discoveryService: discoveryService,
            remoteConfigurationManager: remoteConfigurationManager,
            lineProbeResolver: lineProbeResolver,
            snapshotUploader: debuggerUploader,
            diagnosticsUploader: diagnosticsUploader,
            probeStatusPoller: probeStatusPoller,
            configurationUpdater: configurationUpdater,
            dogStats: statsd);
    }

    private static IDogStatsd GetDogStatsd(TracerSettings tracerSettings, string serviceName)
    {
        IDogStatsd statsd;
        if (FrameworkDescription.Instance.IsWindows()
         && tracerSettings.Exporter.MetricsTransport == TransportType.UDS)
        {
            Log.Information("Metric probes are not supported on Windows when transport type is UDS");
            statsd = new NoOpStatsd();
        }
        else
        {
            statsd = TracerManagerFactory.CreateDogStatsdClient(tracerSettings, serviceName, constantTags: null, prefix: DebuggerSettings.DebuggerMetricPrefix, telemtryFlushInterval: null);
        }

        return statsd;
    }

    private static SnapshotUploader CreateSnapshotUploader(IDiscoveryService discoveryService, DebuggerSettings debuggerSettings, IGitMetadataTagsProvider gitMetadataTagsProvider, IApiRequestFactory apiFactory, SnapshotSink snapshotStatusSink)
    {
        var snapshotBatchUploadApi = DebuggerUploadApiFactory.CreateSnapshotUploadApi(apiFactory, discoveryService, gitMetadataTagsProvider);
        var snapshotBatchUploader = BatchUploader.Create(snapshotBatchUploadApi);

        var debuggerSink = SnapshotUploader.Create(snapshotStatusSink, snapshotBatchUploader, debuggerSettings);

        return debuggerSink;
    }

    private static DiagnosticsUploader CreateDiagnosticsUploader(IDiscoveryService discoveryService, DebuggerSettings debuggerSettings, IGitMetadataTagsProvider gitMetadataTagsProvider, IApiRequestFactory apiFactory, DiagnosticsSink diagnosticsSink)
    {
        var diagnosticsBatchUploadApi = DebuggerUploadApiFactory.CreateDiagnosticsUploadApi(apiFactory, discoveryService, gitMetadataTagsProvider);
        var diagnosticsBatchUploader = BatchUploader.Create(diagnosticsBatchUploadApi);

        var debuggerSink = DiagnosticsUploader.Create(diagnosticsSink, diagnosticsBatchUploader: diagnosticsBatchUploader, debuggerSettings);

        return debuggerSink;
    }

    internal static IDebuggerUploader CreateSymbolsUploader(IDiscoveryService discoveryService, IRcmSubscriptionManager remoteConfigurationManager, string serviceName, TracerSettings tracerSettings, DebuggerSettings settings, IGitMetadataTagsProvider gitMetadataTagsProvider)
    {
        var symbolBatchApi = DebuggerUploadApiFactory.CreateSymbolsUploadApi(GetApiFactory(tracerSettings, true), discoveryService, gitMetadataTagsProvider, serviceName, settings.SymbolDatabaseCompressionEnabled);
        var symbolsUploader = SymbolsUploader.Create(symbolBatchApi, discoveryService, remoteConfigurationManager, tracerSettings, settings, serviceName);
        return symbolsUploader;
    }

    private static IApiRequestFactory GetApiFactory(TracerSettings tracerSettings, bool isMultipart)
    {
        // TODO: we need to be able to update the tracer settings dynamically
        return AgentTransportStrategy.Get(
            tracerSettings.Exporter,
            productName: "debugger",
            tcpTimeout: TimeSpan.FromSeconds(15),
            AgentHttpHeaderNames.MinimalHeaders,
            isMultipart ? () => new MultipartAgentHeaderHelper() : () => new MinimalAgentHeaderHelper(),
            uri => uri);
    }
}
