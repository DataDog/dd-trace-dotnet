// <copyright file="DataStreamsWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.DataStreamsMonitoring.Transport;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Util;
using FluentAssertions;
using MessagePack;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsWriterTests
{
    private const int BucketDurationMs = 1_000; // 1 second
    private const string Environment = "env";
    private const string Service = "service";
    private int _flushCount;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task DoesNotWriteIfNoStats_Periodic(bool? isSupported)
    {
        var bucketDurationMs = 100; // 100 ms
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDurationMs);
        writer.FlushComplete += (_, _) => Interlocked.Increment(ref _flushCount);
        TriggerSupportUpdate(discovery, isSupported);

        await WaitForFlushCount(bucketDurationMs * 10);

        api.Sent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task DoesNotWriteIfNoStats_OnClose(bool? isSupported)
    {
        var bucketDuration = 100_000_000;
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDuration);
        TriggerSupportUpdate(discovery, isSupported);

        await writer.DisposeAsync();

        api.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenSupported_WritesAStatsPointAfterDelay()
    {
        var bucketDurationMs = 100;
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDurationMs);
        TriggerSupportUpdate(discovery, isSupported: true);

        // stats and backlogs will be sent as the same payload
        writer.Add(CreateStatsPoint());
        writer.AddBacklog(CreateBacklogPoint());

        await api.WaitForCount(1, 30_000);

        HasOneOrTwoPoints(api);

        await writer.DisposeAsync();

        HasOneOrTwoPoints(api);
    }

    [Fact]
    public async Task DoesNotCrashWhenWritingAStatsPointFails()
    {
        var bucketDurationMs = 100_000_000;
        var api = new FaultyApi();
        var writer = CreateWriter(api, out var discovery, bucketDurationMs);
        TriggerSupportUpdate(discovery, isSupported: true);

        writer.Add(CreateStatsPoint());

        await writer.DisposeAsync();

        api.Sent.Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task WhenNotSupported_DoesNotWritesAStatsPointAfterDelay(bool? isSupported)
    {
        var bucketDurationMs = 100;
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDurationMs * 1_000_000);
        writer.FlushComplete += (_, _) => Interlocked.Increment(ref _flushCount);
        TriggerSupportUpdate(discovery, isSupported);

        writer.Add(CreateStatsPoint());

        await WaitForFlushCount(bucketDurationMs * 10);

        await writer.DisposeAsync();

        api.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenSupported_WritesAStatsPoint_OnClose()
    {
        var bucketDuration = 100_000_000;
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDuration);
        TriggerSupportUpdate(discovery, isSupported: true);

        writer.Add(CreateStatsPoint());

        await writer.DisposeAsync();

        api.Sent.Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task WhenNotSupported_DoesNotWriteAStatsPoint_OnClose(bool? isSupported)
    {
        var bucketDuration = 100_000_000;
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDuration);
        TriggerSupportUpdate(discovery, isSupported);

        writer.Add(CreateStatsPoint());

        await writer.DisposeAsync();

        api.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenSupported_AllBucketsAreReportedOnClose()
    {
        // using a very long duration here so that we're guaranteed to still
        // be in the "current" bucket when we call flush and close
        var bucketDuration = int.MaxValue;
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDuration);
        TriggerSupportUpdate(discovery, isSupported: true);

        writer.Add(CreateStatsPoint());

        await writer.DisposeAsync();

        api.Sent.Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task WhenNotSupported_DoesNotReportAnyBucketsOnClose(bool? isSupported)
    {
        // using a very long duration here so that we're guaranteed to still
        // be in the "current" bucket when we call flush and close
        var bucketDuration = int.MaxValue;
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDuration);
        TriggerSupportUpdate(discovery, isSupported);

        writer.Add(CreateStatsPoint());

        await writer.DisposeAsync();

        api.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task CanCreateWriterWithDefaultBucket()
    {
        var writer = CreateWriter(new StubApi(), out _, DataStreamsConstants.DefaultBucketDurationMs);
        await writer.DisposeAsync();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, true)]
    public async Task WhenUnSupported_AndBecomesSupported_SendsPointToWriter(bool? initialSupport, bool finalSupport)
    {
        var bucketDurationMs = 100; // 100 ms
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDurationMs);
        TriggerSupportUpdate(discovery, initialSupport);

        writer.Add(CreateStatsPoint());

        // Disabled, so after 2 flushes should have removed the point, and not sent to API
        await WaitForFlushCount(bucketDurationMs * 10);

        api.Sent.Should().BeEmpty();

        TriggerSupportUpdate(discovery, isSupported: finalSupport); // change in support

        writer.Add(CreateStatsPoint());

        await writer.DisposeAsync();

        HasOneOrTwoPoints(api);
    }

    [Fact]
    public async Task WhenSupported_AndBecomesUnSupported_SendsFirstPointToWriter()
    {
        var bucketDurationMs = 100; // 100 ms
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDurationMs);
        TriggerSupportUpdate(discovery, isSupported: true);

        writer.Add(CreateStatsPoint());

        await api.WaitForCount(1, 30_000);

        HasOneOrTwoPoints(api);

        TriggerSupportUpdate(discovery, isSupported: false); // change in support

        writer.Add(CreateStatsPoint());

        await writer.DisposeAsync();

        HasOneOrTwoPoints(api);
    }

    [Fact]
    public async Task GZipsDataWhenSendingToApi()
    {
        var bucketDurationMs = 100_000_000;
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDurationMs);
        TriggerSupportUpdate(discovery, isSupported: true);

        writer.Add(CreateStatsPoint());
        writer.AddBacklog(CreateBacklogPoint());

        await writer.DisposeAsync();

        HasOneOrTwoPoints(api);

        var payloads = new List<MockDataStreamsPayload>();
        foreach (var payload in api.Sent)
        {
            using var compressed = new MemoryStream(payload.Array!);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            await gzip.CopyToAsync(decompressed);

            var result = MessagePackSerializer.Deserialize<MockDataStreamsPayload>(decompressed.GetBuffer());

            payloads.Add(result);
        }

        payloads.Should().OnlyContain(x => x.Env == Environment);
        payloads.Should().OnlyContain(x => x.Service == Service);
        payloads.Should().Contain(x => x.Stats.Any(y => y.Stats != null && y.Stats.Any(z => z.TimestampType == "current")));
        payloads.Should().Contain(x => x.Stats.Any(y => y.Stats != null && y.Stats.Any(z => z.TimestampType == "origin")));
        payloads.Should().Contain(x => x.Stats.Any(y => y.Backlogs != null && y.Backlogs.Any(b => b.Tags.Contains("type:produce"))));
    }

    [Fact]
    public async Task WhenUnSupported_DropsPoints()
    {
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDurationMs: 10_000);
        TriggerSupportUpdate(discovery, isSupported: false);

        // These shouldn't be added
        writer.Add(CreateStatsPoint());
        writer.Add(CreateStatsPoint());
        writer.Add(CreateStatsPoint());
        writer.AddBacklog(CreateBacklogPoint());

        var pointsDropped = writer.PointsDropped;
        pointsDropped.Should().Be(4);

        // enable support
        TriggerSupportUpdate(discovery, isSupported: true);

        // These should be added
        writer.Add(CreateStatsPoint());
        writer.Add(CreateStatsPoint());
        writer.Add(CreateStatsPoint());
        writer.AddBacklog(CreateBacklogPoint());

        writer.PointsDropped.Should().Be(pointsDropped, "Should be unchanged");

        await writer.DisposeAsync();
    }

    [Fact]
    public async Task WhenSupportedUnknown_DoesNotDropPoints()
    {
        var api = new StubApi();
        var writer = CreateWriter(api, out var discovery, bucketDurationMs: 10_000);
        TriggerSupportUpdate(discovery, isSupported: null);

        // These should be added
        writer.Add(CreateStatsPoint());
        writer.Add(CreateStatsPoint());
        writer.Add(CreateStatsPoint());
        writer.AddBacklog(CreateBacklogPoint());

        writer.PointsDropped.Should().Be(0);

        await writer.DisposeAsync();
    }

    private static void HasOneOrTwoPoints(StubApi api)
    {
        // The origin and current points _might_ be sent in separate payloads
        api.Sent.Should().HaveCountGreaterOrEqualTo(1).And.HaveCountLessOrEqualTo(2);
    }

    private static DataStreamsWriter CreateWriter(
        IDataStreamsApi stubApi,
        out DiscoveryServiceMock discoveryService,
        int bucketDurationMs = BucketDurationMs)
    {
        discoveryService = new DiscoveryServiceMock();
        return new DataStreamsWriter(
            new DataStreamsAggregator(
                new DataStreamsMessagePackFormatter(Environment, Service),
                bucketDurationMs),
            stubApi,
            bucketDurationMs: bucketDurationMs,
            discoveryService);
    }

    private static void TriggerSupportUpdate(DiscoveryServiceMock discovery, bool? isSupported)
    {
        if (isSupported == true)
        {
            discovery.TriggerChange();
        }
        else if (isSupported == false)
        {
            discovery.TriggerChange(dataStreamsMonitoringEndpoint: null);
        }
    }

    private static StatsPoint CreateStatsPoint()
        => new StatsPoint(
            edgeTags: new[] { "direction:out", "type:kafka" },
            hash: new PathwayHash((ulong)Math.Abs(ThreadSafeRandom.Shared.Next(int.MaxValue))),
            parentHash: new PathwayHash((ulong)Math.Abs(ThreadSafeRandom.Shared.Next(int.MaxValue))),
            timestampNs: DateTimeOffset.UtcNow.ToUnixTimeNanoseconds(),
            pathwayLatencyNs: 5_000_000_000,
            edgeLatencyNs: 2_000_000_000,
            payloadSizeBytes: 1024);

    private static BacklogPoint CreateBacklogPoint()
        => new BacklogPoint("type:produce", 100, DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

    private async Task WaitForFlushCount(int timeout, int flushCount = 2)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (Volatile.Read(ref _flushCount) < flushCount && (DateTime.UtcNow) > deadline)
        {
            await Task.Delay(100);
        }
    }

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

        public virtual Task<bool> SendAsync(ArraySegment<byte> bytes)
        {
            lock (_sent)
            {
                _sent.Add(bytes);
            }

            return Task.FromResult(true);
        }

        public async Task WaitForCount(int count, int timeoutMs)
        {
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (Sent.Count < count && DateTime.UtcNow < end)
            {
                await Task.Delay(100);
            }
        }
    }

    private class FaultyApi : StubApi
    {
        public override Task<bool> SendAsync(ArraySegment<byte> bytes)
        {
            base.SendAsync(bytes);
            return Task.FromResult(false);
        }
    }
}
