// <copyright file="DataStreamsManualCheckpointOptionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

// Checkpoints set through the manual API with DataStreamsCheckpointOptions, used for stages with
// no instrumented producer (e.g. primary database -> replica -> CDC -> a bridge that produces to Kafka).
public class DataStreamsManualCheckpointOptionsTests
{
    private const long Second = 1_000_000_000;

    [Fact]
    public void ChainedStages_EachEdgeIsTheGapSinceThePreviousStage()
    {
        var dsm = GetDataStreamManager(out var writer);
        var nowNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
        var originalChangeNs = nowNs - (10 * Second);
        var replicaCommitNs = nowNs - (4 * Second);

        dsm.SetManualCheckpoint(null, CheckpointKind.Produce, new EdgeTags(["direction:out", "topic:primary-db", "type:sqlserver"]), originalChangeNs, sourceTimeNs: null, chainFromPrevious: false);
        dsm.SetManualCheckpoint(null, CheckpointKind.Consume, new EdgeTags(["direction:in", "topic:replica-db", "type:sqlserver"]), replicaCommitNs, sourceTimeNs: null, chainFromPrevious: true);
        dsm.SetManualCheckpoint(null, CheckpointKind.Consume, new EdgeTags(["direction:in", "topic:cdc", "type:sqlserver"]), checkpointTimeNs: nowNs, sourceTimeNs: null, chainFromPrevious: true);

        var points = writer.Points.ToArray();
        points.Should().HaveCount(3);

        points[0].ParentHash.Value.Should().Be(0);
        points[0].EdgeLatencyNs.Should().Be(0);

        points[1].ParentHash.Should().Be(points[0].Hash);
        points[1].EdgeLatencyNs.Should().Be(6 * Second);
        points[1].PathwayLatencyNs.Should().Be(6 * Second);

        points[2].ParentHash.Should().Be(points[1].Hash);
        points[2].EdgeLatencyNs.Should().Be(4 * Second);
        points[2].PathwayLatencyNs.Should().Be(10 * Second);
    }

    [Fact]
    public void SourceTimestamp_BackdatesTheFirstEdge_AndTheNextHopIsNotCountedTwice()
    {
        var dsm = GetDataStreamManager(out var writer);
        var sourceNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds() - (5 * Second);

        var context = dsm.SetManualCheckpoint(null, CheckpointKind.Consume, new EdgeTags(["direction:in", "topic:replica-db", "type:sqlserver"]), checkpointTimeNs: null, sourceNs, chainFromPrevious: false);
        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Produce, new EdgeTags(["direction:out", "topic:orders", "type:kafka"]), payloadSizeBytes: 0, timeInQueueMs: 0);

        context.Should().NotBeNull();
        context!.Value.PathwayStart.Should().Be(sourceNs);
        context.Value.EdgeStart.Should().BeGreaterThan(sourceNs + (4 * Second));

        var points = writer.Points.ToArray();
        points.Should().HaveCount(2);
        points[0].EdgeLatencyNs.Should().BeGreaterOrEqualTo(5 * Second);
        points[1].ParentHash.Should().Be(points[0].Hash);
        points[1].EdgeLatencyNs.Should().BeLessThan(Second);
        points[1].PathwayLatencyNs.Should().BeGreaterOrEqualTo(5 * Second);
    }

    [Fact]
    public void MissingTimestamps_FallBackToNow()
    {
        var dsm = GetDataStreamManager(out var writer);

        dsm.SetManualCheckpoint(null, CheckpointKind.Consume, new EdgeTags(["direction:in", "topic:replica-db", "type:sqlserver"]), checkpointTimeNs: null, sourceTimeNs: null, chainFromPrevious: false);

        var point = writer.Points.Should().ContainSingle().Subject;
        point.EdgeLatencyNs.Should().Be(0);
        point.PathwayLatencyNs.Should().Be(0);
    }

    [Fact]
    public void BackdatedCheckpoint_IsBucketedByWallClock()
    {
        var dsm = GetDataStreamManager(out var writer);
        var beforeNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

        dsm.SetManualCheckpoint(null, CheckpointKind.Produce, new EdgeTags(["direction:out", "topic:primary-db", "type:sqlserver"]), beforeNs - (3600 * Second), sourceTimeNs: null, chainFromPrevious: false);

        writer.Points.Should().ContainSingle().Which.TimestampNs.Should().BeGreaterOrEqualTo(beforeNs);
    }

    [Fact]
    public void OriginProduce_WithoutChain_StartsNewPathway_EvenAfterAConsume()
    {
        var dsm = GetDataStreamManager(out var writer);

        // e.g. the previous row's last stage, still remembered in this async flow
        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new EdgeTags(["direction:in", "topic:cdc", "type:sqlserver"]), payloadSizeBytes: 0, timeInQueueMs: 0);
        dsm.SetManualCheckpoint(null, CheckpointKind.Produce, new EdgeTags(["direction:out", "topic:primary-db", "type:sqlserver"]), checkpointTimeNs: null, sourceTimeNs: null, chainFromPrevious: false);

        writer.Points.ToArray()[1].ParentHash.Value.Should().Be(0);
    }

    [Fact]
    public void Produce_WithChain_LinksToThePreviousConsume()
    {
        var dsm = GetDataStreamManager(out var writer);

        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new EdgeTags(["direction:in", "topic:cdc", "type:sqlserver"]), payloadSizeBytes: 0, timeInQueueMs: 0);
        dsm.SetManualCheckpoint(null, CheckpointKind.Produce, new EdgeTags(["direction:out", "topic:orders", "type:kafka"]), checkpointTimeNs: null, sourceTimeNs: null, chainFromPrevious: true);

        var points = writer.Points.ToArray();
        points[1].ParentHash.Should().Be(points[0].Hash);
    }

    [Fact]
    public void ParentInCarrier_WinsOverChainFromPrevious()
    {
        var dsm = GetDataStreamManager(out var writer);
        var carrierParent = dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Produce, new EdgeTags(["direction:out", "topic:a", "type:kafka"]), payloadSizeBytes: 0, timeInQueueMs: 0);
        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new EdgeTags(["direction:in", "topic:b", "type:kafka"]), payloadSizeBytes: 0, timeInQueueMs: 0);

        dsm.SetManualCheckpoint(carrierParent, CheckpointKind.Consume, new EdgeTags(["direction:in", "topic:a", "type:kafka"]), checkpointTimeNs: null, sourceTimeNs: null, chainFromPrevious: true);

        writer.Points.ToArray()[2].ParentHash.Should().Be(carrierParent!.Value.Hash);
    }

    [Fact]
    public void WhenDisabled_ReturnsNull()
    {
        var dsm = new DataStreamsManager(CreateSettings(enabled: false), writer: null, Mock.Of<IDiscoveryService>());

        dsm.SetManualCheckpoint(null, CheckpointKind.Consume, new EdgeTags(["direction:in", "topic:replica-db", "type:sqlserver"]), checkpointTimeNs: null, sourceTimeNs: null, chainFromPrevious: false)
           .Should().BeNull();
    }

    private static DataStreamsManager GetDataStreamManager(out DataStreamsManagerTests.DataStreamsWriterMock writer)
    {
        writer = new DataStreamsManagerTests.DataStreamsWriterMock();
        return new DataStreamsManager(CreateSettings(enabled: true), writer, Mock.Of<IDiscoveryService>());
    }

    private static TracerSettings CreateSettings(bool enabled)
        => TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.Environment, "foo" },
                { ConfigurationKeys.ServiceName, "bar" },
                { ConfigurationKeys.DataStreamsMonitoring.Enabled, enabled.ToString() },
                { ConfigurationKeys.PropagateProcessTags, "false" },
            });
}
