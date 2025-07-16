// <copyright file="TracerHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;

namespace Datadog.Trace.TestHelpers.TestTracer;

internal static class TracerHelper
{
    /// <summary>
    /// Create a test instance of the Tracer, that doesn't use any shared instances
    /// </summary>
    public static ScopedTracer Create(
        TracerSettings settings = null,
        IAgentWriter agentWriter = null,
        ITraceSampler sampler = null,
        IScopeManager scopeManager = null,
        IDogStatsd statsd = null,
        ITelemetryController telemetryController = null,
        IDiscoveryService discoveryService = null) =>
        new(settings, agentWriter, sampler, scopeManager, statsd, discoveryService: discoveryService, telemetryController: telemetryController);

    /// <summary>
    /// Create a test instance of the Tracer, that doesn't use any shared instances
    /// </summary>
    public static ScopedTracer CreateWithFakeAgent(
        TracerSettings settings = null) =>
        new(settings, Mock.Of<IAgentWriter>());
}
