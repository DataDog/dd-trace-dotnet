// <copyright file="ScopedTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.TestHelpers.TestTracer;

internal class ScopedTracer : Tracer, IAsyncDisposable
{
    public ScopedTracer(
        TracerSettings settings = null,
        IAgentWriter agentWriter = null,
        ITraceSampler sampler = null,
        IScopeManager scopeManager = null,
        IDogStatsd statsd = null,
        ITelemetryController telemetryController = null,
        IDiscoveryService discoveryService = null)
        : this(settings, agentWriter, sampler, scopeManager, statsd is null ? null : new TestStatsdManager(statsd), telemetryController, discoveryService)
    {
    }

    public ScopedTracer(
        TracerSettings settings,
        IAgentWriter agentWriter,
        ITraceSampler sampler,
        IScopeManager scopeManager,
        IStatsdManager statsdManager,
        ITelemetryController telemetryController = null,
        IDiscoveryService discoveryService = null)
        : base(settings, agentWriter, sampler, scopeManager, statsdManager, telemetry: telemetryController, discoveryService: discoveryService)
    {
    }

    public ValueTask DisposeAsync() => new(TracerManager.ShutdownAsync());
}
