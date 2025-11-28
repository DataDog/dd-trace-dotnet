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
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Tagging;

public class TraceTags
{
    [Theory]
    [InlineData(SamplingMechanism.Default)]
    [InlineData(SamplingMechanism.AgentRate)]
    [InlineData(SamplingMechanism.LocalTraceSamplingRule)]
    [InlineData(SamplingMechanism.Manual)]
    [InlineData(SamplingMechanism.Asm)]
    public async Task SerializeSamplingMechanismTag(string samplingMechanism)
    {
        // make it so all traces are initially dropped so we can override with keep,
        // otherwise we can't change the sampling mechanism
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.GlobalSamplingRate, 0 } });

        var testApi = new MockApi();
        var agentWriter = new AgentWriter(testApi, statsAggregator: null, statsd: TestStatsdManager.NoOp);
        await using var tracer = TracerHelper.Create(settings, agentWriter, null, null, null);

        using (var scope = tracer.StartActiveInternal("root"))
        {
            var traceContext = scope.Span.Context.TraceContext;
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, samplingMechanism);
        }

        await tracer.FlushAsync();
        var traceChunks = testApi.Wait(TimeSpan.FromSeconds(3));
        var deserializedSpan = traceChunks.Single().Single();
        deserializedSpan.Tags.Should().Contain("_dd.p.dm", samplingMechanism);
    }
}
