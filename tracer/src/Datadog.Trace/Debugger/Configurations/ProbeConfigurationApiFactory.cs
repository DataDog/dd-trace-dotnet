// <copyright file="ProbeConfigurationApiFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;

namespace Datadog.Trace.Debugger.Configurations;

internal static class ProbeConfigurationApiFactory
{
    public static IProbeConfigurationApi Create(ImmutableDebuggerSettings settings, IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
    {
        IProbeConfigurationApi api = settings.ProbeMode switch
        {
            ProbeMode.File => FileProbeConfigurationApi.Create(settings),
            ProbeMode.Agent => RcmProbeConfigurationApi.Create(settings, apiRequestFactory, discoveryService),
            _ => throw new ArgumentOutOfRangeException()
        };

        return api;
    }
}
