// <copyright file="SpanContextDataStreamsManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class SpanContextDataStreamsManagerTests
{
    [Fact]
    public void SetCheckpoint_SetsTheSpanPathwayContext()
    {
        var dsm = GetEnabledDataStreamManager();
        var spanContext = new SpanContext(traceId: 123, spanId: 1234);
        spanContext.PathwayContext.Should().BeNull();

        spanContext.SetCheckpoint(dsm, CheckpointKind.Produce, new[] { "some-edge" }, 100, 0);

        spanContext.PathwayContext.Should().NotBeNull();
    }

    [Fact]
    public void MergePathwayContext_WhenNull_UsesOtherContext()
    {
        var ctx = new PathwayContext();

        var spanContext = new SpanContext(traceId: 123, spanId: 1234);
        spanContext.PathwayContext.Should().BeNull();

        spanContext.MergePathwayContext(ctx);
        spanContext.PathwayContext.Value.Should().Be(ctx);
    }

    [Fact]
    public void MergePathwayContext_WhenOtherContextIsNull_KeepsContext()
    {
        var dsm = GetEnabledDataStreamManager();
        var spanContext = new SpanContext(traceId: 123, spanId: 1234);
        spanContext.SetCheckpoint(dsm,  CheckpointKind.Produce, new[] { "some-edge" }, 100, 0);
        spanContext.PathwayContext.Should().NotBeNull();
        var previous = spanContext.PathwayContext;

        spanContext.MergePathwayContext(null);
        spanContext.PathwayContext.Should().Be(previous);
    }

    [Fact]
    public void MergePathwayContext_WhenOtherContextIsNotNull_KeepsEach50Percent()
    {
        int iterations = 1000_000;
        // When we have a context and there's a new context we pick one randomly
        var dsm = GetEnabledDataStreamManager();
        var spanContext = new SpanContext(traceId: 123, spanId: 1234);
        spanContext.SetCheckpoint(dsm, CheckpointKind.Produce, new[] { "some-edge" }, 100, 0);

        // Make sure we have a different hash for comparison purposes
        while (spanContext.PathwayContext.Value.Hash.Value < (ulong)iterations)
        {
            spanContext.SetCheckpoint(dsm, CheckpointKind.Produce, new[] { "some-edge" }, 100, 0);
        }

        var sameCount = 0;
        var otherCount = 0;
        for (int i = 0; i < iterations; i++)
        {
            var other = new PathwayContext(new PathwayHash((ulong)i), 1234, 5678);
            spanContext.MergePathwayContext(other);
            if (spanContext.PathwayContext.Value.Hash.Value == (ulong)i)
            {
                otherCount++;
            }
            else
            {
                sameCount++;
            }
        }

        sameCount.Should().BeCloseTo(otherCount, (uint)(iterations / 100)); // roughly 1%
    }

    private static DataStreamsManager GetEnabledDataStreamManager()
    {
        var dsm = new DataStreamsManager(
            env: "env",
            defaultServiceName: "service",
            new Mock<IDataStreamsWriter>().Object);
        return dsm;
    }
}
