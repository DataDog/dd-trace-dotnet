// <copyright file="MockDataStreamsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using MessagePack;

namespace Datadog.Trace.TestHelpers.DataStreamsMonitoring;

[MessagePackObject]
public class MockDataStreamsPayload
{
    [Key(nameof(Env))]
    public string Env { get; set; }

    // Service is the service of the application
    [Key(nameof(Service))]
    public string Service { get; set; }

    [Key(nameof(PrimaryTag))]
    public string PrimaryTag { get; set; }

    [Key(nameof(TracerVersion))]
    public string TracerVersion { get; set; }

    [Key(nameof(Lang))]
    public string Lang { get; set; }

    [Key(nameof(Stats))]
    public MockDataStreamsBucket[] Stats { get; set; }

    public static MockDataStreamsPayload Normalize(IImmutableList<MockDataStreamsPayload> dataStreams)
    {
        // This is nasty and hacky, but it's the only way I could get any semblance
        // of snapshots. We could have more than one payload due to the way flushing works,
        // but if we ignore the start times of the buckets, we can group them in a consistent way
        dataStreams.Should().NotBeEmpty();

        // make sure they all have the same top level properties
        var payload = dataStreams.First();
        dataStreams.Should()
                   .OnlyContain(x => x.Env == payload.Env)
                   .And.OnlyContain(x => x.Lang == payload.Lang)
                   .And.OnlyContain(x => x.Service == payload.Service)
                   .And.OnlyContain(x => x.PrimaryTag == payload.PrimaryTag)
                   .And.OnlyContain(x => x.TracerVersion == payload.TracerVersion);

        var currentBucket = new MockDataStreamsBucket { Duration = 10_000_000_000, Start = 1661520120000000000UL };
        var originBucket = new MockDataStreamsBucket { Duration = 10_000_000_000, Start = 1661520120000000000UL };

        var currentBucketStats = new List<MockDataStreamsStatsPoint>();
        var originBucketStats = new List<MockDataStreamsStatsPoint>();
        var backlogs = new List<MockDataStreamsBacklog>();
        foreach (var mockPayload in dataStreams)
        {
            foreach (var bucket in mockPayload.Stats)
            {
                bucket.Duration.Should().Be(expected: 10_000_000_000); // 10s in ns
                bucket.Start.Should().BePositive();

                if (bucket.Stats != null)
                {
                    var buckets = bucket.Stats.First().TimestampType == "current" ? currentBucketStats : originBucketStats;
                    foreach (var bucketStat in bucket.Stats)
                    {
                        if (!buckets.Any(x => x.Hash == bucketStat.Hash && x.ParentHash == bucketStat.ParentHash))
                        {
                            buckets.Add(bucketStat);
                        }
                    }
                }

                if (bucket.Backlogs != null)
                {
                    backlogs.AddRange(bucket.Backlogs);
                }
            }
        }

        // order and reset tag offset values, since
        // there's no guarantee that messages will be routed the same way on every run.
        currentBucket.Backlogs = GroupBacklogs(backlogs);
        currentBucket.Stats = StableSort(currentBucketStats);
        originBucket.Stats = StableSort(originBucketStats);
        payload.Stats = new[] { currentBucket, originBucket };
        // redact the tracer version as we don't want to regenerate snapshots on every version change
        payload.TracerVersion = "<snip>";
        return payload;

        static MockDataStreamsStatsPoint[] StableSort(IReadOnlyCollection<MockDataStreamsStatsPoint> points)
        {
            // sort each bucket by "depth", then by consumer name
            // Ensure a static ordering for the spans
            return points
                  .OrderBy(x => GetRootHashName(x, points))
                  .ThenBy(x => GetHashDepth(x, points))
                  .ThenBy(x => x.Hash)
                  .ToArray();
        }

        static ulong GetRootHashName(MockDataStreamsStatsPoint point, IReadOnlyCollection<MockDataStreamsStatsPoint> allPoints)
        {
            while (point.ParentHash != 0)
            {
                var parent = allPoints.FirstOrDefault(x => x.Hash == point.ParentHash);
                if (parent is null)
                {
                    // no span with the given Parent Id, so treat this one as the root instead
                    break;
                }

                point = parent;
            }

            return point.Hash;
        }

        static int GetHashDepth(MockDataStreamsStatsPoint point, IReadOnlyCollection<MockDataStreamsStatsPoint> allPoints)
        {
            var depth = 0;
            while (point.ParentHash != 0)
            {
                var parent = allPoints.FirstOrDefault(x => x.Hash == point.ParentHash);
                if (parent is null)
                {
                    // no span with the given Parent Id, so treat this one as the root instead
                    break;
                }

                point = parent;
                depth++;
            }

            return depth;
        }

        static MockDataStreamsBacklog[] GroupBacklogs(List<MockDataStreamsBacklog> backlogs)
        {
            var tuples = backlogs.Select(
                s =>
                {
                    Array.Sort(s.Tags);
                    return new Tuple<string, long>(string.Join(",", s.Tags), s.Value);
                });

            var maxByTags = tuples.GroupBy(
                g => g.Item1,
                (tags, values) => tags);

            return maxByTags
                  .OrderBy(o => o)
                  .Select(s => new MockDataStreamsBacklog() { Tags = s.Split(separator: ',') })
                  .ToArray();
        }
    }
}
