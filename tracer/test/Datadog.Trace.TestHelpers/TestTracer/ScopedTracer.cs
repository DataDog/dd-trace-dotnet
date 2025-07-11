// <copyright file="ScopedTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.TestHelpers.TestTracer;

internal class ScopedTracer(
    TracerSettings settings = null,
    IAgentWriter agentWriter = null,
    ITraceSampler sampler = null,
    IScopeManager scopeManager = null,
    IDogStatsd statsd = null,
    ITelemetryController telemetryController = null,
    IDiscoveryService discoveryService = null)
    : Tracer(settings, agentWriter, sampler, scopeManager, statsd, telemetry: telemetryController, discoveryService: discoveryService), IAsyncDisposable
{
    public async ValueTask DisposeAsync() => await TracerManager.ShutdownAsync();
}
