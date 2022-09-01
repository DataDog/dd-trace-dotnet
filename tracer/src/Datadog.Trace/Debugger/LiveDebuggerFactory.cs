// <copyright file="LiveDebuggerFactory.cs" company="Datadog">
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
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Debugger;

internal class LiveDebuggerFactory
{
    public static LiveDebugger Create(IDiscoveryService discoveryService, IRemoteConfigurationManager remoteConfigurationManager, ImmutableExporterSettings exporterSettings, string serviceName)
    {
        var source = GlobalSettings.CreateDefaultConfigurationSource();
        var settings = ImmutableDebuggerSettings.Create(DebuggerSettings.FromSource(source));
        if (!settings.Enabled)
        {
            return LiveDebugger.Create(settings, string.Empty, null, null, null, null, null, null);
        }

        var snapshotStatusSink = SnapshotSink.Create(settings);
        var probeStatusSink = ProbeStatusSink.Create(settings, serviceName);

        var apiFactory = AgentTransportStrategy.Get(
            exporterSettings,
            productName: "debugger",
            tcpTimeout: TimeSpan.FromSeconds(15),
            AgentHttpHeaderNames.MinimalHeaders,
            () => new MinimalAgentHeaderHelper(),
            uri => uri);

        var batchApi = AgentBatchUploadApi.Create(settings, apiFactory, discoveryService);
        var batchUploader = BatchUploader.Create(batchApi);
        var debuggerSink = DebuggerSink.Create(snapshotStatusSink, probeStatusSink, settings, batchUploader);

        var lineProbeResolver = LineProbeResolver.Create();
        var probeStatusPoller = ProbeStatusPoller.Create(settings, probeStatusSink);

        var configurationUpdater = ConfigurationUpdater.Create(settings);
        return LiveDebugger.Create(settings, serviceName, discoveryService, remoteConfigurationManager, lineProbeResolver, debuggerSink, probeStatusPoller, configurationUpdater);
    }
}
