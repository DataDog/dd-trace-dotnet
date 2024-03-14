// <copyright file="StatsDiscovery.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Agent.DiscoveryService;

internal class StatsDiscovery
{
    private readonly IDiscoveryService _discoveryService;

    public StatsDiscovery(IDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
        discoveryService.SubscribeToChanges(HandleConfigUpdate);
    }

    public bool? CanComputeStats { get; private set; } = null;

    public void Dispose()
    {
        _discoveryService.RemoveSubscription(HandleConfigUpdate);
    }

    private void HandleConfigUpdate(AgentConfiguration config)
    {
        CanComputeStats = !string.IsNullOrWhiteSpace(config.StatsEndpoint) && config.ClientDropP0s == true;

        if (CanComputeStats.Value)
        {
            Log.Debug("Stats computation has been enabled.");
        }
        else
        {
            Log.Warning("Stats computation disabled because the detected agent does not support this feature.");
        }
    }
}
