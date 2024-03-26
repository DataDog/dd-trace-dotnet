// <copyright file="DataStreamsMessagePackFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using Datadog.Trace.Vendors.Datadog.Sketches;
using FluentAssertions;
using MessagePack;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsMessagePackFormatterTests
{
    [Fact]
    public void CanRoundTripMessagePackFormat()
    {
        var env = "my-env";
        var service = "service=name";
        var bucketDuration = 10_000_000_000;
        var edgeTags = new[] { "edge-1" };
        var formatter = new DataStreamsMessagePackFormatter(env, service);

        var timeNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

        var bucketStartTimeNs1 = timeNs - (timeNs % bucketDuration);
        var bucketStartTimeNs2 = bucketStartTimeNs1 - 5_000_000_000;

        var pathwaySketch = CreateSketch(5);
        var edgeSketch = CreateSketch(2);
        var payloadSizeSketch = CreateSketch(2);

        var hash1 = new PathwayHash(2);
        var hash2 = new PathwayHash(3);
        var parentHash = new PathwayHash(1);
        List<SerializableStatsBucket> buckets = new()
        {
            new SerializableStatsBucket(
                TimestampType.Current,
                bucketStartTimeNs: bucketStartTimeNs1,
                bucket: new()
                {
                    {
                        hash1.Value, new StatsBucket(
                            edgeTags,
                            hash: hash1,
                            parentHash: parentHash,
                            pathwayLatency: pathwaySketch,
                            edgeLatency: edgeSketch,
                            payloadSize: payloadSizeSketch)
                    },
                }),
            new SerializableStatsBucket(
                TimestampType.Origin,
                bucketStartTimeNs: bucketStartTimeNs2,
                bucket: new()
                {
                    {
                        hash2.Value, new StatsBucket(
                            Array.Empty<string>(),
                            hash: hash2,
                            parentHash: parentHash,
                            pathwayLatency: pathwaySketch,
                            edgeLatency: edgeSketch,
                            payloadSize: payloadSizeSketch)
                    },
                }),
        };

        List<SerializableBacklogBucket> backlogBuckets = new()
        {
            new SerializableBacklogBucket(bucketStartTimeNs1, new()
            {
                { "type:produce", new BacklogBucket("type:produce", 1) }
            }),
            new SerializableBacklogBucket(bucketStartTimeNs2, new()
            {
                { "type:consume", new BacklogBucket("type:consume", 2) }
            }),
        };

        using var ms = new MemoryStream();
        formatter.Serialize(ms, bucketDurationNs: bucketDuration, statsBuckets: buckets, backlogBuckets);

        var data = new ArraySegment<byte>(ms.GetBuffer());

        var result = MessagePackSerializer.Deserialize<MockDataStreamsPayload>(data);

        var pathwayBytes = new byte[pathwaySketch.ComputeSerializedSize()];
        using var ms1 = new MemoryStream(pathwayBytes);
        pathwaySketch.Serialize(ms1);
        var edgeBytes = new byte[edgeSketch.ComputeSerializedSize()];
        using var ms2 = new MemoryStream(edgeBytes);
        edgeSketch.Serialize(ms2);
        var payloadSizeBytes = new byte[payloadSizeSketch.ComputeSerializedSize()];
        using var ms3 = new MemoryStream(payloadSizeBytes);
        payloadSizeSketch.Serialize(ms3);

        var expected = new MockDataStreamsPayload
        {
            Env = env,
            Service = service,
            Lang = "dotnet",
            TracerVersion = TracerConstants.AssemblyVersion,
            Stats = new MockDataStreamsBucket[]
            {
                new()
                {
                    Duration = (ulong)bucketDuration,
                    Start = (ulong)bucketStartTimeNs1,
                    Backlogs = new MockDataStreamsBacklog[]
                    {
                        new()
                        {
                            Tags = new[] { "type:produce" },
                            Value = 1,
                        }
                    }
                },
                new()
                {
                    Duration = (ulong)bucketDuration,
                    Start = (ulong)bucketStartTimeNs2,
                    Backlogs = new MockDataStreamsBacklog[]
                    {
                        new()
                        {
                            Tags = new[] { "type:consume" },
                            Value = 2,
                        }
                    }
                },
                new()
                {
                    Duration = (ulong)bucketDuration,
                    Start = (ulong)bucketStartTimeNs1,
                    Stats = new MockDataStreamsStatsPoint[]
                    {
                        new()
                        {
                            EdgeTags = edgeTags,
                            Hash = hash1.Value,
                            ParentHash = parentHash.Value,
                            EdgeLatency = edgeBytes,
                            PathwayLatency = pathwayBytes,
                            PayloadSize = payloadSizeBytes,
                            TimestampType = "current",
                        }
                    }
                },
                new()
                {
                    Duration = (ulong)bucketDuration,
                    Start = (ulong)bucketStartTimeNs2,
                    Stats = new MockDataStreamsStatsPoint[]
                    {
                        new()
                        {
                            EdgeTags = null,
                            Hash = hash2.Value,
                            ParentHash = parentHash.Value,
                            EdgeLatency = edgeBytes,
                            PathwayLatency = pathwayBytes,
                            PayloadSize = payloadSizeBytes,
                            TimestampType = "origin",
                        }
                    }
                }
            }
        };

        result.Should().BeEquivalentTo(expected);
    }

    private static DDSketch CreateSketch(params int[] values)
    {
        // don't actually need to pool them for these tests
        var sketch = new DDSketchPool().Get();
        foreach (var value in values)
        {
            sketch.Add(value);
        }

        return sketch;
    }
}
