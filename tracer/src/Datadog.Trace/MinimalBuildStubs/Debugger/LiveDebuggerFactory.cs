// <copyright file="LiveDebuggerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Debugger;

internal class LiveDebuggerFactory
{
    private static readonly LiveDebugger Instance = new();

    public static LiveDebugger Create(
        IDiscoveryService discoveryService,
        IRcmSubscriptionManager instance,
        TracerSettings settings,
        string serviceName,
        ITelemetryController tracerManagerTelemetry,
        DebuggerSettings debuggerSettings,
        IGitMetadataTagsProvider tracerManagerGitMetadataTagsProvider)
        => Instance;
}
