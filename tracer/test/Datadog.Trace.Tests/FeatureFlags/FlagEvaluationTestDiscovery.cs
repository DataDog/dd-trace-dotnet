// <copyright file="FlagEvaluationTestDiscovery.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Tests.FeatureFlags;

// Drives the real module without network polling. Like shared discovery, subscriptions replay the
// last configuration and callbacks can already be in flight when a subscription is removed.
internal sealed class FlagEvaluationTestDiscovery : IDiscoveryService
{
    private readonly object _lock = new();
    private Action<AgentConfiguration>? _callbacks;
    private AgentConfiguration? _configuration;

    public void SubscribeToChanges(Action<AgentConfiguration> callback)
    {
        AgentConfiguration? configuration;
        lock (_lock)
        {
            _callbacks += callback;
            configuration = _configuration;
        }

        if (configuration is not null)
        {
            callback(configuration);
        }
    }

    public void RemoveSubscription(Action<AgentConfiguration> callback)
    {
        lock (_lock)
        {
            _callbacks -= callback;
        }
    }

    public void SetCurrentConfigStateHash(string configStateHash)
    {
    }

    public Task DisposeAsync() => Task.CompletedTask;

    internal static FlagEvaluationTestDiscovery Available(TracerSettings settings)
    {
        var discovery = new FlagEvaluationTestDiscovery();
        discovery.Publish(settings.Manager.InitialExporterSettings);
        return discovery;
    }

    internal void Publish(ExporterSettings settings, string? endpoint = "evp_proxy/v2")
    {
        var configuration = new AgentConfiguration(
            configurationEndpoint: null,
            debuggerEndpoint: null,
            debuggerV2Endpoint: null,
            diagnosticsEndpoint: null,
            symbolDbEndpoint: null,
            agentVersion: "7.84.0",
            statsEndpoint: null,
            dataStreamsMonitoringEndpoint: null,
            eventPlatformProxyEndpoint: endpoint,
            telemetryProxyEndpoint: null,
            tracerFlareEndpoint: null,
            containerTagsHash: null,
            clientDropP0: false,
            spanMetaStructs: false,
            spanEvents: false,
            discoverySettings: settings,
            eventPlatformProxySupportsEvpOriginHeaders: true);
        Action<AgentConfiguration>? callbacks;
        lock (_lock)
        {
            _configuration = configuration;
            callbacks = _callbacks;
        }

        callbacks?.Invoke(configuration);
    }
}
