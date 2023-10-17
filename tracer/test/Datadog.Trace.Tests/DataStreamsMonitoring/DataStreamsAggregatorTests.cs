// <copyright file="DataStreamsAggregatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions;
using Datadog.Trace.Vendors.Datadog.Sketches;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsAggregatorTests
{
    private const long OneSecondNs = 1_000_000_000;
    private const long OneKB = 1024;
    private const int BucketDurationMs = DataStreamsConstants.DefaultBucketDurationMs; // 10s
    private const long BucketDurationNs = ((long)BucketDurationMs) * 1_000_000;

    private static readonly long T1 = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

    // Set t2 to be some 40 seconds after the t1, but also account for bucket alignments,
    // otherwise the possible StatsPayload would change depending on when the test is run.
    private static readonly long T2 = BucketStartTimeForTimestamp(T1 + (40 * OneSecondNs)) + (6 * OneSecondNs);

    // Based on the go aggregator_tests
    // https://cs.github.com/DataDog/data-streams-go/blob/6772b163707c0a8ecc8c9a3b28e0dab7e0cf58d4/datastreams/aggregator_test.go#L28

    [Fact]
    public void Aggregator_DoesNotFlushCurrentStats()
    {
        var aggregator = CreateAggregatorWithData(T1, T2);

        // flush at t2 doesn't flush points at t2 (current bucket)
        // so only the last point is included
        var statsToWrite = aggregator.Export(T2);

        // compare expected, bit messy, but works
        var currentStats = statsToWrite
                          .Where(x => x.TimestampType == TimestampType.Current)
                          .Should()
                          .ContainSingle()
                          .Subject;
        AssertStats(currentStats, TimestampType.Current, BucketStartTimeForTimestamp(T1));
        AssertBucket(currentStats, hash: 2, CreateSketch(5), CreateSketch(2));

        var originStats = statsToWrite
                          .Where(x => x.TimestampType == TimestampType.Origin)
                          .Should()
                          .ContainSingle()
                          .Subject;
        // // origin timestamp subtracts the pathway latency
        AssertStats(originStats, TimestampType.Origin, BucketStartTimeForTimestamp(T1 - (5 * OneSecondNs)));
        AssertBucket(originStats, hash: 2, CreateSketch(5), CreateSketch(2));

        // Check that only one backlog is flushed at T2
        var backlogsToWrite = aggregator.ExportBacklogs(T2);
        backlogsToWrite.Should().ContainSingle();
        backlogsToWrite.First().BucketStartTimeNs.Should().Be(BucketStartTimeForTimestamp(T1));
    }

    [Fact]
    public void Aggregator_SecondSerializationClearsBuckets()
    {
        var aggregator = CreateAggregatorWithData(T1, T2);

        using var ms1 = new MemoryStream();
        var bytesWritten = aggregator.Serialize(ms1, maxBucketFlushTimeNs: T2);
        bytesWritten.Should().BeTrue();

        // serializing clears the stats, so shouldn't write anything on the second attempt;
        using var ms2 = new MemoryStream();
        bytesWritten = aggregator.Serialize(ms2, maxBucketFlushTimeNs: T2);
        bytesWritten.Should().BeFalse();
    }

    [Fact]
    public void Aggregator_FlushesStats()
    {
        var aggregator = CreateAggregatorWithData(T1, T2);

        // flush at t2 doesn't flush points at t2 (current bucket)
        // so only the last point is included
        var statsToWrite = aggregator.Export(T2);
        var backlogsToWrite = aggregator.ExportBacklogs(T2);
        backlogsToWrite.Should().ContainSingle();
        backlogsToWrite[0].Bucket.Should().ContainSingle();
        backlogsToWrite[0].Bucket.First().Key.Should().Be("type:produce,topic:test");
        backlogsToWrite[0].Bucket.First().Value.Value.Should().Be(100L);

        // we always clear after calling Export
        aggregator.Clear(statsToWrite, backlogsToWrite);

        // Now advance for bucket duration + 1 and flush again
        statsToWrite = aggregator.Export(T2 + BucketDurationNs + OneSecondNs);

        // should contain only one backlog
        backlogsToWrite = aggregator.ExportBacklogs(T2 + BucketDurationNs + OneSecondNs);
        backlogsToWrite.Should().ContainSingle();
        backlogsToWrite[0].Bucket.Should().ContainSingle();
        backlogsToWrite[0].Bucket.First().Key.Should().Be("type:consume,topic:test");
        backlogsToWrite[0].Bucket.First().Value.Value.Should().Be(200L);

        // compare expected
        var stats = statsToWrite[0];
        AssertStats(stats, TimestampType.Current, BucketStartTimeForTimestamp(T2));
        AssertBucket(stats, hash: 2, CreateSketch(1, 5), CreateSketch(1, 2));
        AssertBucket(stats, hash: 3, CreateSketch(5), CreateSketch(2));

        stats = statsToWrite[1];
        // // origin timestamp subtracts the pathway latency
        AssertStats(stats, TimestampType.Origin, BucketStartTimeForTimestamp(T2 - (5 * OneSecondNs)));
        AssertBucket(stats, hash: 2, CreateSketch(1, 5), CreateSketch(1, 2));
        AssertBucket(stats, hash: 3, CreateSketch(5), CreateSketch(2));
    }

    private static DataStreamsAggregator CreateAggregatorWithData(long t1, long t2)
    {
        var aggregator = new DataStreamsAggregator(
            new DataStreamsMessagePackFormatter("env", "service"),
            BucketDurationMs);

        aggregator.Add(
            new StatsPoint(
                edgeTags: new[] { "edge-1" },
                hash: new PathwayHash(2),
                parentHash: new PathwayHash(1),
                timestampNs: t2,
                pathwayLatencyNs: OneSecondNs,
                edgeLatencyNs: OneSecondNs,
                payloadSizeBytes: OneKB));

        aggregator.Add(
            new StatsPoint(
                edgeTags: new[] { "edge-1" },
                hash: new PathwayHash(2),
                parentHash: new PathwayHash(1),
                timestampNs: t2,
                pathwayLatencyNs: 5 * OneSecondNs,
                edgeLatencyNs: 2 * OneSecondNs,
                payloadSizeBytes: OneKB * 2));

        aggregator.Add(
            new StatsPoint(
                edgeTags: new[] { "edge-1" },
                hash: new PathwayHash(3), // different hash
                parentHash: new PathwayHash(1),
                timestampNs: t2,
                pathwayLatencyNs: 5 * OneSecondNs,
                edgeLatencyNs: 2 * OneSecondNs,
                payloadSizeBytes: OneKB * 2));

        aggregator.Add(
            new StatsPoint(
                edgeTags: new[] { "edge-1" },
                hash: new PathwayHash(2),
                parentHash: new PathwayHash(1),
                timestampNs: t1, // different start time
                pathwayLatencyNs: 5 * OneSecondNs,
                edgeLatencyNs: 2 * OneSecondNs,
                payloadSizeBytes: OneKB * 2));
        aggregator.AddBacklog(new BacklogPoint("type:produce,topic:test", 100, t1));
        aggregator.AddBacklog(new BacklogPoint("type:consume,topic:test", 200, t2));
        return aggregator;
    }

    private static void AssertStats(SerializableStatsBucket stats, TimestampType timestampType, long startTime)
    {
        stats.TimestampType.Should().Be(timestampType);
        stats.BucketStartTimeNs.Should().Be(startTime);
    }

    private static void AssertBucket(SerializableStatsBucket stats, ulong hash, byte[] pathway, byte[] edge)
    {
        stats.Bucket.Should().ContainKey(hash);

        var bucket = stats.Bucket[hash];
        bucket.EdgeTags.Should().ContainSingle("edge-1");
        bucket.Hash.Value.Should().Be(hash);
        bucket.ParentHash.Value.Should().Be(1);
        Serialize(bucket.PathwayLatency).Should().BeEquivalentTo(pathway);
        Serialize(bucket.EdgeLatency).Should().BeEquivalentTo(edge);
    }

    private static long BucketStartTimeForTimestamp(long timestampNs)
        => timestampNs - (timestampNs % BucketDurationNs);

    private static byte[] CreateSketch(params int[] values)
    {
        // don't actually need to pool them for these tests
        var sketch = new DDSketchPool().Get();
        foreach (var value in values)
        {
            sketch.Add(value);
        }

        return Serialize(sketch);
    }

    private static byte[] Serialize(DDSketch sketch)
    {
        var bytes = new byte[sketch.ComputeSerializedSize()];
        using var ms = new MemoryStream(bytes);
        sketch.Serialize(ms);
        return bytes;
    }
}
