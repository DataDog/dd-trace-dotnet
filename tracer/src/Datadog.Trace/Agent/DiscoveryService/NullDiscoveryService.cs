// <copyright file="NullDiscoveryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.DiscoveryService;

internal class NullDiscoveryService : IDiscoveryService
{
    public static readonly NullDiscoveryService Instance = new();

    private NullDiscoveryService()
    {
    }

    public void SubscribeToChanges(Action<AgentConfiguration> callback)
    {
    }

    public void RemoveSubscription(Action<AgentConfiguration> callback)
    {
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
