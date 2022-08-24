// <copyright file="TraceTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Tagging;

public class TraceTags
{
    [Theory]
    [InlineData(SamplingMechanism.Default)]
    [InlineData(SamplingMechanism.AgentRate)]
    [InlineData(SamplingMechanism.TraceSamplingRule)]
    [InlineData(SamplingMechanism.Manual)]
    [InlineData(SamplingMechanism.Asm)]
    public void SerializeSamplingMechanismTag(int samplingMechanism)
    {
        // set up the trace
        var tracer = new Mock<IDatadogTracer>();
        var traceContext = new TraceContext(tracer.Object);
        traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, samplingMechanism);

        // create a span
        var spanContext = new SpanContext(SpanContext.None, traceContext, "service1");
        var span = new Span(spanContext, start: null);
        traceContext.AddSpan(span);

        var deserializedSpan = SerializeSpan(span);

        deserializedSpan.Tags.Should().Contain("_dd.p.dm", $"-{samplingMechanism}");
    }

    private static MockSpan SerializeSpan(Span span)
    {
        var buffer = Array.Empty<byte>();

        // use vendored MessagePack to serialize
        Vendors.MessagePack.MessagePackSerializer.Serialize(ref buffer, 0, span, SpanFormatterResolver.Instance);

        // use nuget MessagePack to deserialize
        return global::MessagePack.MessagePackSerializer.Deserialize<MockSpan>(buffer);
    }
}
