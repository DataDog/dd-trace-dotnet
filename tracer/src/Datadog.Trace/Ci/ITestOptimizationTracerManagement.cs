// <copyright file="ITestOptimizationTracerManagement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Ci;

internal interface ITestOptimizationTracerManagement
{
    EventPlatformProxySupport EventPlatformProxySupport { get; }

    TestOptimizationTracerManager? Manager { get; }

    bool UseLockedTracerManager { get; }

    IDiscoveryService DiscoveryService { get; }

    EventPlatformProxySupport IsEventPlatformProxySupportedByAgent(IDiscoveryService discoveryService);

    EventPlatformProxySupport EventPlatformProxySupportFromEndpointUrl(string? eventPlatformProxyEndpoint);

    IApiRequestFactory GetRequestFactory(TracerSettings settings);

    IApiRequestFactory GetRequestFactory(TracerSettings tracerSettings, TimeSpan timeout);

    string GetServiceNameFromRepository(string? repository);
}
