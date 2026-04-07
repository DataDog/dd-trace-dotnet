// <copyright file="ProcessTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Tagging;

public class ProcessTagsTests
{
    private readonly MockApi _testApi;

    public ProcessTagsTests()
    {
        _testApi = new MockApi();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessTags_Only_In_First_Span(bool enabled)
    {
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection { { ConfigurationKeys.PropagateProcessTags, enabled ? "true" : "false" } }));
        var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);
        await using var tracer = TracerHelper.Create(settings, agentWriter);

        using (tracer.StartActiveInternal("A"))
        using (tracer.StartActiveInternal("AA"))
        {
        }

        // other trace
        using (tracer.StartActiveInternal("B"))
        using (tracer.StartActiveInternal("BB"))
        {
        }

        await tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        traceChunks.Should().HaveCount(2); // 2 (small) traces = 2 chunks
        if (enabled)
        {
            // process tags written only to first span of first chunk
            traceChunks[0][0].Tags.Should().ContainKey(Tags.ProcessTags);
        }
        else
        {
            traceChunks[0][0].Tags.Should().NotContainKey(Tags.ProcessTags);
        }

        traceChunks.SelectMany(x => x) // flatten
            .Skip(1) // exclude first item that we just checked above
            .Should()
            .AllSatisfy(s => s.Tags.Should().NotContainKey(Tags.ProcessTags));
    }
}
