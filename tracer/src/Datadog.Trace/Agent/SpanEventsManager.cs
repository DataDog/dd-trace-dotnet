// <copyright file="SpanEventsManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Logging;

#nullable enable

namespace Datadog.Trace.Agent;

internal sealed class SpanEventsManager : ISpanEventsManager
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanEventsManager>();
    private readonly IDiscoveryService? _discoveryService;
    private bool _nativeSpanEventsEnabled;

    public SpanEventsManager(IDiscoveryService? discoveryService)
    {
        _discoveryService = discoveryService;
    }

    public bool NativeSpanEventsEnabled => _nativeSpanEventsEnabled;

    public void Start()
    {
        if (_discoveryService is not null)
        {
            _discoveryService.SubscribeToChanges(HandleConfigUpdate);
        }
    }

    public void Dispose()
    {
        if (_discoveryService is not null)
        {
            _discoveryService.RemoveSubscription(HandleConfigUpdate);
        }
    }

    private void HandleConfigUpdate(AgentConfiguration config)
    {
        if (config.SpanEvents != _nativeSpanEventsEnabled)
        {
            _nativeSpanEventsEnabled = config.SpanEvents;
            Log.Debug("Native span events support updated: {Enabled}", _nativeSpanEventsEnabled);
        }
    }
}
