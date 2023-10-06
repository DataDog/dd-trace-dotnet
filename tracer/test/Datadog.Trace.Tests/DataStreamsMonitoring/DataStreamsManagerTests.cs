// <copyright file="DataStreamsManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsManagerTests
{
    [Fact]
    public void WhenDisabled_DoesNotInjectContext()
    {
        var dsm = GetDataStreamManager(false, out _);

        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

        dsm.InjectPathwayContext(context, headers);

        headers.Values.Should().BeEmpty();
    }

    [Fact]
    public void WhenEnabled_InjectsContext()
    {
        var dsm = GetDataStreamManager(true, out _);

        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

        dsm.InjectPathwayContext(context, headers);

        headers.Values.Should().NotBeEmpty();
    }

    [Fact]
    public void WhenDisabled_DoesNotExtractContext()
    {
        var enabledDsm = GetDataStreamManager(true, out _);
        var disabledDsm = GetDataStreamManager(false, out _);

        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

        enabledDsm.InjectPathwayContext(context, headers);
        headers.Values.Should().NotBeEmpty();

        disabledDsm.ExtractPathwayContext(headers).Should().BeNull();
    }

    [Fact]
    public void WhenEnabled_ExtractsContext()
    {
        var dsm = GetDataStreamManager(true, out _);

        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1_234_000_000, 5_678_000_000);

        dsm.InjectPathwayContext(context, headers);
        headers.Values.Should().NotBeEmpty();

        var extracted = dsm.ExtractPathwayContext(headers);
        extracted.Should().NotBeNull();
        extracted.Value.Hash.Value.Should().Be(context.Hash.Value);
        extracted.Value.PathwayStart.Should().Be(context.PathwayStart);
        extracted.Value.EdgeStart.Should().Be(context.EdgeStart);
    }

    [Fact]
    public void WhenEnabled_AndNoContext_ReturnsNewContext()
    {
        var dsm = GetDataStreamManager(true, out _);

        var context = dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new[] { "some-tags" }, 100, 100);
        context.Should().NotBeNull();
    }

    [Fact]
    public void WhenEnabled_TimeInQueueIsUsedForPipelineStart()
    {
        long latencyMs = 100;
        var latencyNs = latencyMs * 1_000_000;

        var dsm = GetDataStreamManager(true, out var writer);
        var context = dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new[] { "some-tags" }, 100, latencyMs);

        context.Should().NotBeNull();
        context?.EdgeStart.Should().Be(context.Value.PathwayStart);
        (DateTimeOffset.UtcNow.ToUnixTimeNanoseconds() - context?.EdgeStart).Should().BeGreaterOrEqualTo(latencyNs);

        writer.Points.Should().ContainSingle();
        writer.Points.TryPeek(out var point).Should().BeTrue();

        point.EdgeLatencyNs.Should().Be(latencyNs);
        point.PathwayLatencyNs.Should().Be(latencyNs);
    }

    [Fact]
    public void WhenEnabled_TimeInQueueIsNotUsedForSecondCheckpoint()
    {
        long latencyMs = 100;
        var latencyNs = latencyMs * 1_000_000;

        var dsm = GetDataStreamManager(true, out var writer);
        var parent = dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new[] { "some-tags" }, 100, latencyMs);
        Thread.Sleep(1);
        dsm.SetCheckpoint(parentPathway: parent, CheckpointKind.Consume, new[] { "some-tags" }, 100, latencyMs);

        writer.Points.Should().HaveCount(2);
        writer.Points.TryDequeue(out var _).Should().BeTrue();
        writer.Points.TryDequeue(out var point).Should().BeTrue();

        point.EdgeLatencyNs.Should().BeGreaterThan(latencyNs);
        point.PathwayLatencyNs.Should().BeGreaterThan(latencyNs);
    }

    [Fact]
    public void WhenEnabled_PayloadSizeIsUsed()
    {
        var dsm = GetDataStreamManager(true, out var writer);
        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new[] { "some-tags" }, 100, 0);

        writer.Points.Should().ContainSingle();
        writer.Points.TryPeek(out var point).Should().BeTrue();
        point.PayloadSizeBytes.Should().Be(100);
    }

    [Fact]
    public void WhenEnabled_AndNoContext_HashShouldUseParentHashOfZero()
    {
        var env = "foo";
        var service = "bar";
        var edgeTags = new[] { "some-tags" };
        var dsm = GetDataStreamManager(true, out _);

        var context = dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, edgeTags, 100, 100);
        context.Should().NotBeNull();

        var baseHash = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null);
        var nodeHash = HashHelper.CalculateNodeHash(baseHash, edgeTags);
        var hash = HashHelper.CalculatePathwayHash(nodeHash, parentHash: new PathwayHash(0));

        context.Value.Hash.Value.Should().Be(hash.Value);
    }

    [Fact]
    public void WhenEnabled_AndHashContext_HashShouldUseParentHash()
    {
        var env = "foo";
        var service = "bar";
        var edgeTags = new[] { "some-tags" };
        var dsm = GetDataStreamManager(true, out _);
        var parent = new PathwayContext(new PathwayHash(123), 12340000, 56780000);

        var context = dsm.SetCheckpoint(parent, CheckpointKind.Consume, edgeTags, 100, 100);
        context.Should().NotBeNull();

        var baseHash = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null);
        var nodeHash = HashHelper.CalculateNodeHash(baseHash, edgeTags);
        var hash = HashHelper.CalculatePathwayHash(nodeHash, parentHash: parent.Hash);

        context.Value.Hash.Value.Should().Be(hash.Value);
    }

    [Fact]
    public void WhenDisabled_SetCheckpoint_ReturnsNull()
    {
        var dsm = GetDataStreamManager(false, out _);
        var parent = new PathwayContext(new PathwayHash(123), 12340000, 56780000);

        var context = dsm.SetCheckpoint(parent, CheckpointKind.Consume, new[] { "some-tags" }, 100, 100);
        context.Should().BeNull();
    }

    [Fact]
    public void WhenEnabled_SetCheckpoint_SetsSpanTags()
    {
        var dsm = GetDataStreamManager(true, out _);
        var span = new Span(new SpanContext(traceId: 123, spanId: 456), DateTimeOffset.UtcNow);

        span.SetDataStreamsCheckpoint(dsm,  CheckpointKind.Produce, new[] { "direction:out" }, 100, 0);
        span.Tags.GetTag("pathway.hash").Should().NotBeNull();
    }

    [Fact]
    public async Task DisposeAsync_DisablesDsm()
    {
        var dsm = GetDataStreamManager(true, out _);
        var parent = new PathwayContext(new PathwayHash(123), 12340000, 56780000);

        dsm.IsEnabled.Should().BeTrue();

        await dsm.DisposeAsync();
        dsm.IsEnabled.Should().BeFalse();

        var context = dsm.SetCheckpoint(parent, CheckpointKind.Consume, new[] { "some-tags" }, 100, 100);
        context.Should().BeNull();
    }

    [Fact]
    public async Task WhenDisabled_DoesNotSendPointsToWriter()
    {
        var dsm = GetDataStreamManager(enabled: false, out var writer);
        writer.Should().BeNull(); // can't send points to it, because it's null!

        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new[] { "edge" }, 100, 100);

        await dsm.DisposeAsync();
    }

    [Fact]
    public async Task WhenEnabled_SendsPointsToWriter()
    {
        var dsm = GetDataStreamManager(enabled: true, out var writer);

        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new[] { "edge" }, 100, 100);

        await dsm.DisposeAsync();

        writer.Points.Should().ContainSingle();
    }

    [Fact]
    public async Task WhenDisposed_DisposesWriter()
    {
        var dsm = GetDataStreamManager(enabled: true, out var writer);

        await dsm.DisposeAsync();

        writer.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task WhenDisposedTwice_DisposesWriterOnce()
    {
        var dsm = GetDataStreamManager(enabled: true, out var writer);

        var task = dsm.DisposeAsync();
        var task2 = dsm.DisposeAsync();

        await Task.WhenAll(task, task2);

        writer.DisposeCount.Should().Be(1);
    }

    private static DataStreamsManager GetDataStreamManager(bool enabled, out DataStreamsWriterMock writer)
    {
        writer = enabled ? new DataStreamsWriterMock() : null;
        return new DataStreamsManager(
            env: "foo",
            defaultServiceName: "bar",
            writer);
    }

    internal class DataStreamsWriterMock : IDataStreamsWriter
    {
        private int _disposeCount;

        public ConcurrentQueue<StatsPoint> Points { get; } = new();

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public void Add(in StatsPoint point)
        {
            Points.Enqueue(point);
        }

        public async Task DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            await Task.Yield();
        }
    }
}
