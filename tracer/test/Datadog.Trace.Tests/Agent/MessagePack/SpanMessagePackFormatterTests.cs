// <copyright file="SpanMessagePackFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent.MessagePack;

public class SpanMessagePackFormatterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TraceId128_PropagatedTag(bool generate128BitTraceId)
    {
        var mockApi = new MockApi();
        var settings = new TracerSettings { TraceId128BitGenerationEnabled = generate128BitTraceId };
        var agentWriter = new AgentWriter(mockApi, statsAggregator: null, statsd: null, spanSampler: null);
        var tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null, NullTelemetryController.Instance, NullDiscoveryService.Instance);

        using (var rootScope = tracer.StartActive("root"))
        {
        }

        await tracer.ForceFlushAsync();
        var traceChunks = mockApi.Wait(TimeSpan.FromSeconds(1));
        var span = traceChunks[0][0];
        var tagValue = span.GetTag("_dd.p.tid");

        if (generate128BitTraceId)
        {
            // tag is added if missing when serializing
            HexString.TryParseUInt64(tagValue, out var traceIdUpperValue).Should().BeTrue();
            traceIdUpperValue.Should().BeGreaterThan(0);
        }
        else
        {
            // tag is not added
            tagValue.Should().BeNull();
        }
    }
}
