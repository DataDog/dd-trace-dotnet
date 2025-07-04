// <copyright file="ScopedTracerFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.TestHelpers.TestTracer;

public class ScopedTracerFixture : IAsyncDisposable
{
    private readonly IList<ScopedTracer> _tracers = new List<ScopedTracer>();

    public async ValueTask DisposeAsync()
    {
        foreach (var scopedTracer in _tracers)
        {
            await scopedTracer.DisposeAsync();
        }
    }

    internal ScopedTracer BuildScopedTracer(TracerSettings settings = null, IAgentWriter agentWriter = null, ITraceSampler sampler = null, IScopeManager scopeManager = null, IDogStatsd statsd = null)
    {
        var scopedTracer = new ScopedTracer(settings, agentWriter, sampler, scopeManager, statsd);
        _tracers.Add(scopedTracer);
        return scopedTracer;
    }
}
