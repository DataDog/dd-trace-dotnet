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
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, generate128BitTraceId } });
        var agentWriter = new AgentWriter(mockApi, statsAggregator: null, statsd: null);
        var tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null, NullTelemetryController.Instance, NullDiscoveryService.Instance);

        using (_ = tracer.StartActive("root"))
        {
            using (_ = tracer.StartActive("child"))
            {
            }
        }

        await tracer.FlushAsync();
        var traceChunks = mockApi.Wait(TimeSpan.FromSeconds(1));

        var span0 = traceChunks[0][0];
        var tagValue0 = span0.GetTag("_dd.p.tid");

        var span1 = traceChunks[0][1];
        var tagValue1 = span1.GetTag("_dd.p.tid");

        if (generate128BitTraceId)
        {
            // tag is added to first span of every chunk
            HexString.TryParseUInt64(tagValue0, out var traceIdUpperValue).Should().BeTrue();
            traceIdUpperValue.Should().BeGreaterThan(0);

            // not the second span
            tagValue1.Should().BeNull();
        }
        else
        {
            // tag is not added anywhere
            tagValue0.Should().BeNull();
            tagValue1.Should().BeNull();
        }
    }
}
