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

        spanContext.SetCheckpoint(dsm, CheckpointKind.Produce, new[] { "some-edge" }, payloadSizeBytes: 100, timeInQueueMs: 0, parent: null);

        spanContext.PathwayContext.Should().NotBeNull();
    }

    [Fact]
    public void SetCheckpoint_BatchProduceDoesNotCreateChain()
    {
        var dsm = GetEnabledDataStreamManager();
        var spanContext = new SpanContext(traceId: 123, spanId: 1234);
        spanContext.PathwayContext.Should().BeNull();

        spanContext.SetCheckpoint(dsm, CheckpointKind.Produce, new[] { "some-edge" }, payloadSizeBytes: 100, timeInQueueMs: 0, parent: null);

        spanContext.PathwayContext.Should().NotBeNull();
        var firstPathwayHash = spanContext.PathwayContext.Value.Hash;

        spanContext.SetCheckpoint(dsm, CheckpointKind.Produce, new[] { "some-edge" }, payloadSizeBytes: 100, timeInQueueMs: 0, parent: null);

        spanContext.PathwayContext.Should().NotBeNull();
        // we used the same parameters, so we should have the same hash
        // this would be false if the first pathway was considered as the parent of the second one.
        spanContext.PathwayContext.Value.Hash.Should().BeEquivalentTo(firstPathwayHash);
    }

    [Fact]
    public void SetCheckpoint_BatchConsumeDoesNotCreateChain()
    {
        var dsm = GetEnabledDataStreamManager();
        var spanContext = new SpanContext(traceId: 123, spanId: 1234);
        spanContext.PathwayContext.Should().BeNull();
        var parentCtx = new PathwayContext(new PathwayHash(value: 12), pathwayStartNs: 5, edgeStartNs: 8);

        spanContext.SetCheckpoint(dsm, CheckpointKind.Consume, new[] { "some-edge" }, payloadSizeBytes: 100, timeInQueueMs: 0, parentCtx);

        spanContext.PathwayContext.Should().NotBeNull();
        var firstPathwayHash = spanContext.PathwayContext.Value.Hash;

        spanContext.SetCheckpoint(dsm, CheckpointKind.Consume, new[] { "some-edge" }, payloadSizeBytes: 100, timeInQueueMs: 0, parentCtx);

        spanContext.PathwayContext.Should().NotBeNull();
        // we used the same parameters and the same parent hash, so we should have the same hash
        // this would be false if the first pathway was considered as the parent of the second one.
        spanContext.PathwayContext.Value.Hash.Should().BeEquivalentTo(firstPathwayHash);
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
