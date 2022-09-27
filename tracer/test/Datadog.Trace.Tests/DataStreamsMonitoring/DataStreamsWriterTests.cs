// <copyright file="DataStreamsWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.DataStreamsMonitoring.Transport;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsWriterTests
{
    private const int BucketDurationMs = 1_000; // 1 second

    [Fact]
    public async Task DoesNotWriteIfNoStats_Periodic()
    {
        var bucketDurationMs = 100; // 100 ms
        var api = new StubApi();
        var writer = CreateWriter(api, bucketDurationMs * 1_000_000);

        await Task.Delay(bucketDurationMs * 10);

        api.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotWriteIfNoStats_OnClose()
    {
        var bucketDuration = 100_000_000; // 100 ms
        var api = new StubApi();
        var writer = CreateWriter(api, bucketDuration);

        await writer.DisposeAsync();

        api.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WritesAStatsPointAfterDelay()
    {
        var bucketDurationMs = 100; // 100 ms
        var api = new StubApi();
        var writer = CreateWriter(api, bucketDurationMs * 1_000_000);

        writer.Add(CreateStatsPoint());

        await Task.Delay(bucketDurationMs * 10);

        await writer.DisposeAsync();

        api.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task WritesAStatsPoint_OnClose()
    {
        var bucketDuration = 100_000_000; // 100 ms
        var api = new StubApi();
        var writer = CreateWriter(api, bucketDuration);

        writer.Add(CreateStatsPoint());

        await writer.DisposeAsync();

        api.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task AllBucketsAreReportedOnClose()
    {
        // using a very long duration here so that we're guaranteed to still
        // be in the "current" bucket when we call flush and close
        var bucketDuration = int.MaxValue;
        var api = new StubApi();
        var writer = CreateWriter(api, bucketDuration);

        writer.Add(CreateStatsPoint());

        await writer.DisposeAsync();

        api.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task CanCreateWriterWithDefaultBucket()
    {
        var writer = CreateWriter(new StubApi(), DataStreamsConstants.DefaultBucketDurationMs);
        await writer.DisposeAsync();
    }

    private static DataStreamsWriter CreateWriter(IDataStreamsApi stubApi, int bucketDurationMs = BucketDurationMs)
    {
        return new DataStreamsWriter(
            new DataStreamsAggregator(new DataStreamsMessagePackFormatter("env", "service"), bucketDurationMs),
            stubApi,
            bucketDurationMs: bucketDurationMs);
    }

    private static StatsPoint CreateStatsPoint()
        => new StatsPoint(
            edgeTags: new[] { "type:internal" },
            hash: new PathwayHash((ulong)Math.Abs(ThreadSafeRandom.Next(int.MaxValue))),
            parentHash: new PathwayHash((ulong)Math.Abs(ThreadSafeRandom.Next(int.MaxValue))),
            timestampNs: DateTimeOffset.UtcNow.ToUnixTimeNanoseconds(),
            pathwayLatencyNs: 5_000_000_000,
            edgeLatencyNs: 2_000_000_000);

    private class StubApi : IDataStreamsApi
    {
        private readonly List<ArraySegment<byte>> _sent = new();

        public List<ArraySegment<byte>> Sent
        {
            get
            {
                lock (_sent)
                {
                    return _sent.ToList();
                }
            }
        }

        public Task<bool> SendAsync(ArraySegment<byte> bytes)
        {
            lock (_sent)
            {
                _sent.Add(bytes);
            }

            return Task.FromResult(true);
        }
    }
}
