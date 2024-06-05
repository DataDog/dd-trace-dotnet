// <copyright file="TraceTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Tagging;

public class TraceTags
{
    private readonly Tracer _tracer;
    private readonly MockApi _testApi;

    public TraceTags()
    {
        // make it so all traces are initially dropped so we can override with keep,
        // otherwise we can't change the sampling mechanism
        var settings = new TracerSettings { GlobalSamplingRate = 0 };

        _testApi = new MockApi();
        var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: null);
        _tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
    }

    [Theory]
    [InlineData(SamplingMechanism.Default)]
    [InlineData(SamplingMechanism.AgentRate)]
    [InlineData(SamplingMechanism.LocalTraceSamplingRule)]
    [InlineData(SamplingMechanism.Manual)]
    [InlineData(SamplingMechanism.Asm)]
    public async Task SerializeSamplingMechanismTag(int samplingMechanism)
    {
        using (var scope = _tracer.StartActiveInternal("root"))
        {
            var traceContext = scope.Span.Context.TraceContext;
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, samplingMechanism);
        }

        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();
        var deserializedSpan = traceChunks.Single().Single();
        deserializedSpan.Tags.Should().Contain("_dd.p.dm", $"-{samplingMechanism}");
    }
}
