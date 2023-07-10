// <copyright file="LiveDebuggerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.StatsdClient;
using Datadog.Trace.Vendors.StatsdClient.Transport;

namespace Datadog.Trace.Debugger;

internal class LiveDebuggerFactory
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LiveDebuggerFactory));

    public static LiveDebugger Create(IDiscoveryService discoveryService, IRcmSubscriptionManager remoteConfigurationManager, ImmutableTracerSettings tracerSettings, string serviceName, ITelemetryController telemetry)
    {
        var settings = DebuggerSettings.FromDefaultSource();
        if (!settings.Enabled)
        {
            telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: false, error: null);
            Log.Information("Live Debugger is disabled. To enable it, please set DD_DYNAMIC_INSTRUMENTATION_ENABLED environment variable to 'true'.");
            return LiveDebugger.Create(settings, string.Empty, null, null, null, null, null, null, null, null);
        }

        var snapshotSlicer = SnapshotSlicer.Create(settings);
        var snapshotStatusSink = SnapshotSink.Create(settings, snapshotSlicer);
        var probeStatusSink = ProbeStatusSink.Create(serviceName, settings);

        var apiFactory = AgentTransportStrategy.Get(
            tracerSettings.ExporterInternal,
            productName: "debugger",
            tcpTimeout: TimeSpan.FromSeconds(15),
            AgentHttpHeaderNames.MinimalHeaders,
            () => new MinimalAgentHeaderHelper(),
            uri => uri);

        var batchApi = AgentBatchUploadApi.Create(apiFactory, discoveryService);
        var batchUploader = BatchUploader.Create(batchApi);
        var debuggerSink = DebuggerSink.Create(snapshotStatusSink, probeStatusSink, batchUploader, settings);

        var lineProbeResolver = LineProbeResolver.Create();
        var probeStatusPoller = ProbeStatusPoller.Create(probeStatusSink, settings);

        var symbolUploader = SymbolUploader.Create(batchApi, settings.MaxSymbolSizeToUpload);
        var symbolExtractor = SymbolExtractor.Create(serviceName, symbolUploader);

        var configurationUpdater = ConfigurationUpdater.Create(tracerSettings.EnvironmentInternal, tracerSettings.ServiceVersionInternal);

        IDogStatsd statsd;
        if (FrameworkDescription.Instance.IsWindows()
            && tracerSettings.ExporterInternal.MetricsTransport == TransportType.UDS)
        {
            Log.Information("Metric probes are not supported on Windows when transport type is UDS");
            statsd = new NoOpStatsd();
        }
        else
        {
            statsd = TracerManagerFactory.CreateDogStatsdClient(tracerSettings, serviceName, constantTags: null, DebuggerSettings.DebuggerMetricPrefix);
        }

        telemetry.ProductChanged(TelemetryProductType.DynamicInstrumentation, enabled: true, error: null);
        return LiveDebugger.Create(settings, serviceName, discoveryService, remoteConfigurationManager, lineProbeResolver, debuggerSink, symbolExtractor, probeStatusPoller, configurationUpdater, statsd);
    }
}
