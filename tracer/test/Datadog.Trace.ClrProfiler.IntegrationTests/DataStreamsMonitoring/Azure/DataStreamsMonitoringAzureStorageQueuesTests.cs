// <copyright file="DataStreamsMonitoringAzureStorageQueuesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Azure;

public class DataStreamsMonitoringAzureStorageQueuesTests : TracingIntegrationTest
{
    public DataStreamsMonitoringAzureStorageQueuesTests(ITestOutputHelper output)
        : base("Samples.DataStreams.AzureStorageQueues", output)
    {
    }

    public static IEnumerable<object[]> GetPackageVersions() =>
        from packageVersionArray in new string[] { string.Empty }
        select new string[] { packageVersionArray };

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
        span.Tags["span.kind"] switch
        {
            SpanKinds.Producer or SpanKinds.Consumer when span.Tags.TryGetValue("messaging.system", out var messagingSystem) && messagingSystem == "azurestoragequeues"
                => Result.DefaultSuccess,
            _ => throw new ArgumentException($"span.tags are not supported for the Azure Storage Queues integration: {span.Tags["span.kind"]}", nameof(span)),
        };

    [SkippableTheory]
    [MemberData(nameof(GetPackageVersions))]
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Trait("Category", "EndToEnd")]
    public async Task SubmitsDsmMetrics(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            agent.SpanFilters.Add(static s => s.Tags.TryGetValue("messaging.system", out var value) && value == "azurestoragequeues");

            var spans = await agent.WaitForSpansAsync(6);
            spans.Should().HaveCount(6);

            var taggedSpans = spans.Where(s => s.Tags.ContainsKey("pathway.hash"));
            taggedSpans.Should().HaveCount(6);

            var producerSpans = spans.Where(s => s.Tags["span.kind"] == SpanKinds.Producer).ToList();
            var consumerSpans = spans.Where(s => s.Tags["span.kind"] == SpanKinds.Consumer).ToList();
            producerSpans.Should().HaveCount(3);
            consumerSpans.Should().HaveCount(3);

            var points = CurrentBucketPoints(agent.DataStreams);
            points.Should().NotBeEmpty();

            var producePoints = points.Where(p => p.EdgeTags.Contains("direction:out")).ToList();
            var consumePoints = points.Where(p => p.EdgeTags.Contains("direction:in")).ToList();
            producePoints.Should().NotBeEmpty();
            consumePoints.Should().NotBeEmpty();

            foreach (var point in points)
            {
                point.EdgeTags.Should().Contain("type:azure-storage-queue");
                point.EdgeTags.Should().Contain("topic:dsm-test-queue");
                point.PayloadSize.Should().NotBeEmpty();
            }

            SumCheckpointCounts(producePoints).Should().Be(3);
            SumCheckpointCounts(consumePoints).Should().Be(3);

            var produceHashes = producePoints.Select(p => p.Hash).ToHashSet();
            var linkedConsumePoints = consumePoints.Where(p => p.ParentHash != 0 && produceHashes.Contains(p.ParentHash)).ToList();
            linkedConsumePoints.Should().NotBeEmpty();
            SumCheckpointCounts(linkedConsumePoints).Should().Be(2);
        }
    }

    private static List<MockDataStreamsStatsPoint> CurrentBucketPoints(IImmutableList<MockDataStreamsPayload> payloads)
    {
        var points = new List<(ulong Start, MockDataStreamsStatsPoint Point)>();
        foreach (var payload in payloads)
        {
            foreach (var bucket in payload.Stats)
            {
                if (bucket.Stats is null)
                {
                    continue;
                }

                foreach (var point in bucket.Stats)
                {
                    if (point.TimestampType == "current")
                    {
                        points.Add((bucket.Start, point));
                    }
                }
            }
        }

        return points
            .GroupBy(x => new { x.Start, x.Point.Hash, x.Point.ParentHash })
            .Select(g => g.First().Point)
            .ToList();
    }

    private static double SumCheckpointCounts(IEnumerable<MockDataStreamsStatsPoint> points)
        => points.Sum(p => ReadSketchCount(p.PathwayLatency));

    private static double ReadSketchCount(byte[] sketch)
    {
        var position = 0;
        var count = 0D;

        while (position < sketch.Length)
        {
            var (tag, next) = ReadVarint(sketch, position);
            position = next;
            var field = (int)(tag >> 3);
            var wireType = (int)(tag & 7);

            if (wireType == 0)
            {
                (_, position) = ReadVarint(sketch, position);
            }
            else if (wireType == 1)
            {
                if (field == 4)
                {
                    count += BitConverter.ToDouble(sketch, position);
                }

                position += 8;
            }
            else if (wireType == 2)
            {
                var (length, after) = ReadVarint(sketch, position);
                position = after;
                if (field is 2 or 3)
                {
                    count += ReadStoreCount(sketch, position, position + (int)length);
                }

                position += (int)length;
            }
            else
            {
                throw new InvalidOperationException($"Unexpected wire type {wireType} in a serialized sketch.");
            }
        }

        return count;

        static double ReadStoreCount(byte[] buffer, int start, int end)
        {
            var position = start;
            var count = 0D;

            while (position < end)
            {
                var (tag, next) = ReadVarint(buffer, position);
                position = next;
                var field = (int)(tag >> 3);
                var wireType = (int)(tag & 7);

                if (wireType == 0)
                {
                    (_, position) = ReadVarint(buffer, position);
                }
                else if (wireType == 2 && field == 2)
                {
                    var (length, after) = ReadVarint(buffer, position);
                    position = after;
                    for (var offset = 0; offset < (int)length; offset += sizeof(double))
                    {
                        count += BitConverter.ToDouble(buffer, position + offset);
                    }

                    position += (int)length;
                }
                else if (wireType == 1)
                {
                    position += 8;
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected wire type {wireType} in a serialized store.");
                }
            }

            return count;
        }
    }

    private static (uint Value, int Next) ReadVarint(byte[] buffer, int position)
    {
        var value = 0U;
        var shift = 0;

        while (position < buffer.Length)
        {
            var b = buffer[position++];
            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return (value, position);
            }

            shift += 7;
        }

        throw new InvalidOperationException("Truncated varint in a serialized sketch.");
    }
}
