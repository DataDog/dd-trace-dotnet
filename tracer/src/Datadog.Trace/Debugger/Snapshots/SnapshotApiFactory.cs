// <copyright file="SnapshotApiFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;

namespace Datadog.Trace.Debugger.Snapshots;

internal static class SnapshotApiFactory
{
    public static ISnapshotApi Create(ImmutableDebuggerSettings settings, IApiRequestFactory apiRequestFactory, DiscoveryService discoveryService)
    {
        ISnapshotApi api = settings.ProbeMode switch
        {
            ProbeMode.File => AgentSnapshotApi.Create(settings, apiRequestFactory, discoveryService),
            ProbeMode.Backend => BackendSnapshotApi.Create(settings, apiRequestFactory),
            ProbeMode.Agent => AgentSnapshotApi.Create(settings, apiRequestFactory, discoveryService),
            _ => throw new ArgumentOutOfRangeException()
        };

        return api;
    }
}
