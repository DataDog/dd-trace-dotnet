// <copyright file="TraceContextPropertyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Tagging;

public class TraceContextPropertyTests
{
    private readonly Tracer _tracer;
    private readonly MockApi _testApi;

    public TraceContextPropertyTests()
    {
        _testApi = new MockApi();

        var settings = new TracerSettings();
        var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: null);
        _tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
    }

    [Theory]
    [InlineData(SamplingPriorityValues.UserReject)]
    [InlineData(SamplingPriorityValues.AutoReject)]
    [InlineData(SamplingPriorityValues.AutoKeep)]
    [InlineData(SamplingPriorityValues.UserKeep)]
    [InlineData(-10)]
    [InlineData(100)]
    public async Task SamplingPriority(int samplingPriority)
    {
        var traceContext = CreateTrace(_tracer);
        traceContext.SetSamplingPriority(samplingPriority);

        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();
        var spans = traceChunks.SelectMany(s => s).ToArray();

        spans.Where(s => s.ParentId > 0)
             .Should()
             .HaveCount(3)
             .And.OnlyContain(s => !s.Metrics.ContainsKey("_sampling_priority_v1"));

        spans.Where(s => s.ParentId is null or 0)
             .Should()
             .HaveCount(1)
             .And.OnlyContain(s => s.Metrics["_sampling_priority_v1"] == samplingPriority);
    }

    [Theory]
    [InlineData(null, null)]                           // null
    [InlineData("", null)]                             // empty
    [InlineData(" ", null)]                            // whitespace
    [InlineData("test", "test")]                       // normal
    [InlineData("Caps and Spaces", "Caps and Spaces")] // adding this now so it fails later when we start normalizing this
    public async Task Env(string beforeEnv, string afterEnv)
    {
        var traceContext = CreateTrace(_tracer);
        traceContext.Environment = beforeEnv;

        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        traceChunks.Should().HaveCount(1);    // 1 trace chunk
        traceChunks[0].Should().HaveCount(4); // 4 spans

        if (afterEnv is null)
        {
            traceChunks[0].Should().OnlyContain(s => !s.Tags.ContainsKey("env"));
        }
        else
        {
            traceChunks[0].Should().OnlyContain(s => s.Tags["env"] == afterEnv);
        }
    }

    [Theory]
    [InlineData(null, null)]                           // null
    [InlineData("", null)]                             // empty
    [InlineData(" ", null)]                            // whitespace
    [InlineData("test", "test")]                       // normal
    [InlineData("Caps and Spaces", "Caps and Spaces")] // adding this now so it fails later when we start normalizing this
    public async Task Version(string beforeVersion, string afterVersion)
    {
        using (var scope = _tracer.StartActive("root"))
        {
            var traceContext = ((Scope)scope).Span.Context.TraceContext;
            traceContext.ServiceVersion = beforeVersion;
        }

        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        traceChunks.Should().HaveCount(1);    // 1 trace chunk
        traceChunks[0].Should().HaveCount(1); // 1 span

        if (afterVersion is null)
        {
            traceChunks[0].Should().OnlyContain(s => !s.Tags.ContainsKey("env"));
        }
        else
        {
            traceChunks[0].Should().OnlyContain(s => s.Tags["version"] == afterVersion);
        }
    }

    private static TraceContext CreateTrace(Tracer tracer)
    {
        TraceContext traceContext;

        using (var scope = tracer.StartActive("root"))
        {
            traceContext = ((Scope)scope).Span.Context.TraceContext;

            using (_ = tracer.StartActive("child"))
            {
                using (_ = tracer.StartActive("child"))
                {
                }
            }

            using (_ = tracer.StartActive("child"))
            {
            }
        }

        return traceContext;
    }
}
