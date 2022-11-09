// <copyright file="SamplingPriorityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Tagging;

public class SamplingPriorityTests
{
    private readonly Tracer _tracer;
    private readonly MockApi _testApi;

    public SamplingPriorityTests()
    {
        _testApi = new MockApi();

        var settings = new TracerSettings();
        var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: null, spanSampler: null);
        _tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
    }

    [Theory]
    [InlineData(SamplingPriorityValues.UserReject)]
    [InlineData(SamplingPriorityValues.AutoReject)]
    [InlineData(SamplingPriorityValues.AutoKeep)]
    [InlineData(SamplingPriorityValues.UserKeep)]
    [InlineData(-10)]
    [InlineData(100)]
    public async Task SerializeSamplingMechanismTag(int samplingPriority)
    {
        using (var scope = _tracer.StartActive("root"))
        {
            var traceContext = ((Scope)scope).Span.Context.TraceContext;
            traceContext.SetSamplingPriority(samplingPriority);
        }

        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        traceChunks.Should().HaveCount(1);    // 1 trace chunk
        traceChunks[0].Should().HaveCount(1); // 1 span
        traceChunks[0][0].Metrics.Should().Contain("_sampling_priority_v1", samplingPriority);
    }
}
