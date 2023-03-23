// <copyright file="TraceId128BitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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

        context.PropagatedTags.GetTag(Tags.Propagated.TraceIdUpper).Should().BeNull();
    }
}
