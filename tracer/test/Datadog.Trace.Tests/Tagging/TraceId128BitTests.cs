// <copyright file="TraceId128BitTests.cs" company="Datadog">
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

namespace Datadog.Trace.Tests.Tagging;

public class TraceId128BitTests
{
    [Fact]
    public void StandaloneSpanContext_128Bit_TraceId()
    {
        var context = new SpanContext(
            traceId: new TraceId(0x1234567890abcdef, 0x1122334455667788),
            spanId: 1UL,
            samplingPriority: SamplingPriorityValues.UserKeep,
            serviceName: null,
            origin: "rum");

        context.AddMissingPropagatedTags();
        context.PropagatedTags.GetTag(Tags.Propagated.TraceIdUpper).Should().Be("1234567890abcdef");
    }

    [Fact]
    public void StandaloneSpanContext_64Bit_TraceId()
    {
        var context = new SpanContext(
            traceId: new TraceId(0, 0x1122334455667788),
            spanId: 1UL,
            samplingPriority: SamplingPriorityValues.UserKeep,
            serviceName: null,
            origin: "rum");

        context.AddMissingPropagatedTags();
        context.PropagatedTags.Should().BeNull();
    }

    [Theory]
    [InlineData(0x1234567890abcdef, 0x1122334455667788, "1234567890abcdef1122334455667788")]
    [InlineData(0, 0x1122334455667788, "00000000000000001122334455667788")]
    public void TraceIdSpanTag(ulong upper, ulong lower, string expected)
    {
        var traceId = new TraceId(upper, lower);
        var trace = new TraceContext(tracer: null);
        var propagatedContext = new SpanContext(traceId, spanId: 1, samplingPriority: null, serviceName: null, origin: null);
        var childContext = new SpanContext(propagatedContext, trace, serviceName: null);
        var span = new Span(childContext, start: null);

        span.GetTag(Tags.TraceId).Should().Be(expected);
    }

    [Fact]
    public async Task PropagatedTag_Is_Added_During_Serialization()
    {
        var mockApi = new MockApi();
        var settings = new TracerSettings { TraceId128BitGenerationEnabled = true };
        var agentWriter = new AgentWriter(mockApi, statsAggregator: null, statsd: null, spanSampler: null);
        var tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null, NullTelemetryController.Instance, NullDiscoveryService.Instance);

        using (var rootScope = tracer.StartActive("root"))
        {
        }

        await tracer.ForceFlushAsync();
        var traceChunks = mockApi.Wait(TimeSpan.FromSeconds(1));

        // TODO: verify if this should be the root span or the first span (or if it depends on partial flushing)
        var span = traceChunks[0][0];
        var traceIdUpper = span.GetTag("_dd.p.tid");

        HexString.TryParseUInt64(traceIdUpper, out var traceIdUpperValue).Should().BeTrue();
        traceIdUpperValue.Should().BeGreaterThan(0);
    }
}
