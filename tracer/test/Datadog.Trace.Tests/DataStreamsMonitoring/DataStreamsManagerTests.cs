// <copyright file="DataStreamsManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions;
using FluentAssertions;
using Moq;
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
    public void WhenEnabled_TracksBacklog()
    {
        const string tags = "tag:value";
        const long value = 100;

        var dsm = GetDataStreamManager(true, out var writer);
        dsm.TrackBacklog(tags, value);

        var point = writer.BacklogPoints.Should().ContainSingle().Subject;
        point.Value.Should().Be(value);
        point.Tags.Should().Be(tags);
    }

    [Fact]
    public void WhenEnabled_TracksTransactions()
    {
        var dsm = GetDataStreamManager(true, out var writer);
        dsm.TrackTransaction("transaction-id", "checkpoint");

        writer.DataStreamsTransactions.Size().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WhenEnabled_TimeInQueueIsNotUsedForSecondCheckpoint()
    {
        long latencyMs = 100;
        var latencyNs = latencyMs * 1_000_000;

        var dsm = GetDataStreamManager(true, out var writer);
        var parent = dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new[] { "some-tags" }, 100, latencyMs);
        await Task.Delay(1);
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

        var baseHash = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null, processTags: null, containerTagsHash: null);
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

        var baseHash = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null, processTags: null, containerTagsHash: null);
        var nodeHash = HashHelper.CalculateNodeHash(baseHash, edgeTags);
        var hash = HashHelper.CalculatePathwayHash(nodeHash, parentHash: parent.Hash);

        context.Value.Hash.Value.Should().Be(hash.Value);
    }

    [Fact]
    public void ProcessTagsUsedInBaseHash()
    {
        var env = "foo";
        var service = "bar";

        var hashWithout = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null, processTags: null, containerTagsHash: null);
        var hashWith = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null, "hello:world", containerTagsHash: null);

        hashWith.Value.Should().NotBe(hashWithout.Value);
    }

    [Fact]
    public void ContainerTagsHashUsedInBaseHash()
    {
        var env = "foo";
        var service = "bar";

        var hashWithout = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null, processTags: "hello:world", containerTagsHash: null);
        var hashWith = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null, processTags: "hello:world", "12345ABCDE");

        hashWith.Value.Should().NotBe(hashWithout.Value);
    }

    [Fact]
    public void ContainerTagsHashNotUsedWithoutProcessTags()
    {
        var env = "foo";
        var service = "bar";

        var hashWithout = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null, processTags: null, containerTagsHash: null);
        var hashWith = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null, processTags: null, "12345ABCDE");

        hashWith.Value.Should().Be(hashWithout.Value);
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
        var span = TestSpanExtensions.CreateSpan(new SpanContext(traceId: 123, spanId: 456), DateTimeOffset.UtcNow);

        span.SetDataStreamsCheckpoint(dsm, CheckpointKind.Produce, new[] { "direction:out" }, 100, 0);
        span.Tags.GetTag("pathway.hash").Should().NotBeNull();
    }

    [Fact]
    public void WhenEnabled_TrackTransaction_AddsTransactionAndTagsSpan()
    {
        var dsm = GetDataStreamManager(true, out var writer);
        var span = TestSpanExtensions.CreateSpan(new SpanContext(traceId: 123, spanId: 456), DateTimeOffset.UtcNow);

        span.TrackTransaction(dsm, "tx-abc", "some-checkpoint");

        writer.DataStreamsTransactions.GetDataAndReset().Should().NotBeEmpty();
        span.Tags.GetTag("dsm.transaction.id").Should().Be("tx-abc");
    }

    [Fact]
    public void WhenDisabled_TrackTransaction_DoesNothing()
    {
        var dsm = GetDataStreamManager(false, out var writer);
        var span = TestSpanExtensions.CreateSpan(new SpanContext(traceId: 123, spanId: 456), DateTimeOffset.UtcNow);

        span.TrackTransaction(dsm, "tx-abc", "some-checkpoint");

        span.Tags.GetTag("dsm.transaction.id").Should().BeNull();
        // writer is null when DSM is disabled, so nothing could have been enqueued
        writer.Should().BeNull();
    }

    [Fact]
    public void WhenInDefaultState_TrackTransaction_DoesNotSetTag()
    {
        // DSM is "in default state" when DD_DATA_STREAMS_MONITORING_ENABLED is absent from config.
        // IsTransactionTrackingEnabled = !IsInDefaultState && IsEnabled, so even with a live writer
        // the tag must not be set and nothing should be enqueued.
        var writer = new DataStreamsWriterMock();
        var settings = TracerSettings.Create(new()
        {
            { ConfigurationKeys.Environment, "foo" },
            { ConfigurationKeys.ServiceName, "bar" },
            // DD_DATA_STREAMS_MONITORING_ENABLED intentionally absent → IsInDefaultState = true
        });
        var dsm = new DataStreamsManager(settings, writer, Mock.Of<IDiscoveryService>());
        dsm.IsInDefaultState.Should().BeTrue("precondition: DSM must be in default state");

        var span = TestSpanExtensions.CreateSpan(new SpanContext(traceId: 123, spanId: 456), DateTimeOffset.UtcNow);

        span.TrackTransaction(dsm, "tx-abc", "some-checkpoint");

        span.Tags.GetTag("dsm.transaction.id").Should().BeNull();
        writer.DataStreamsTransactions.Size().Should().Be(0);
    }

    [Fact]
    public void WhenManagerIsNull_TrackTransaction_DoesNothing()
    {
        var span = TestSpanExtensions.CreateSpan(new SpanContext(traceId: 123, spanId: 456), DateTimeOffset.UtcNow);

        var act = () => span.TrackTransaction(null, "tx-abc", "some-checkpoint");
        act.Should().NotThrow();
    }

    [Fact]
    public async Task WhenEnabled_OneConsumeTwoProduceUsesTwiceConsumePathway()
    {
        var dsm = GetDataStreamManager(enabled: true, out var writer);

        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Consume, new[] { "in" }, payloadSizeBytes: 100, timeInQueueMs: 100);

        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Produce, new[] { "out" }, payloadSizeBytes: 100, timeInQueueMs: 100);
        dsm.SetCheckpoint(parentPathway: null, CheckpointKind.Produce, new[] { "out" }, payloadSizeBytes: 100, timeInQueueMs: 100);

        await dsm.DisposeAsync();

        writer.Points.Should().HaveCount(expected: 3);
        var points = writer.Points.ToArray();
        // checking that points are in the expected order
        points[0].EdgeTags.Should().Contain("in");
        points[1].EdgeTags.Should().Contain("out");
        points[2].EdgeTags.Should().Contain("out");
        // both produces should be considered as children of the consume
        points[1].ParentHash.Should().BeEquivalentTo(points[0].Hash);
        points[2].ParentHash.Should().BeEquivalentTo(points[0].Hash);
        // as a result, they should have the same hash (because their tags are the same too)
        points[1].Hash.Should().BeEquivalentTo(points[2].Hash);
        // just checking that the produce had a different hash
        points[0].Hash.Should().NotBeSameAs(points[1].Hash);
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

    [Fact]
    public void GetOrCreateEdgeTags_ReturnsSameArrayReference_WhenCalledTwiceWithSameKey()
    {
        var dsm = GetDataStreamManager(true, out _);
        var key = new ProduceEdgeTagCacheKey("cluster1", "topic1");

        var first = dsm.GetOrCreateEdgeTags(key, static k => [$"kafka_cluster_id:{k.ClusterId}", $"topic:{k.Topic}", "type:kafka"]);
        var second = dsm.GetOrCreateEdgeTags(key, static k => [$"kafka_cluster_id:{k.ClusterId}", $"topic:{k.Topic}", "type:kafka"]);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOrCreateEdgeTags_ReturnsDifferentArrayReferences_ForDifferentKeys()
    {
        var dsm = GetDataStreamManager(true, out _);

        var forTopic1 = dsm.GetOrCreateEdgeTags(new ProduceEdgeTagCacheKey(string.Empty, "topic1"), static k => [$"topic:{k.Topic}", "type:kafka"]);
        var forTopic2 = dsm.GetOrCreateEdgeTags(new ProduceEdgeTagCacheKey(string.Empty, "topic2"), static k => [$"topic:{k.Topic}", "type:kafka"]);

        forTopic2.Should().NotBeSameAs(forTopic1);
    }

    [Fact]
    public void GetOrCreateEdgeTags_KafkaConsume_ReturnsSameArrayReference_WhenCalledTwiceWithSameKey()
    {
        var dsm = GetDataStreamManager(true, out _);
        var key = new ConsumeEdgeTagCacheKey("group1", "topic1", "cluster1");

        var first = dsm.GetOrCreateEdgeTags(key, static k => [$"group:{k.GroupId}", $"topic:{k.Topic}", $"kafka_cluster_id:{k.ClusterId}"]);
        var second = dsm.GetOrCreateEdgeTags(key, static k => [$"group:{k.GroupId}", $"topic:{k.Topic}", $"kafka_cluster_id:{k.ClusterId}"]);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOrCreateEdgeTags_RabbitMQProduce_ReturnsSameArrayReference_WhenCalledTwiceWithSameKey()
    {
        var dsm = GetDataStreamManager(true, out _);
        var key = new RabbitMQProduceEdgeTagCacheKey("exchange1", string.Empty, HasRoutingKey: true);

        var first = dsm.GetOrCreateEdgeTags(key, static k => [$"exchange:{k.Exchange}", "has_routing_key:true", "type:rabbitmq"]);
        var second = dsm.GetOrCreateEdgeTags(key, static k => [$"exchange:{k.Exchange}", "has_routing_key:true", "type:rabbitmq"]);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOrCreateEdgeTags_RabbitMQConsume_ReturnsSameArrayReference_WhenCalledTwiceWithSameKey()
    {
        var dsm = GetDataStreamManager(true, out _);
        var key = new RabbitMQConsumeEdgeTagCacheKey("queue1");

        var first = dsm.GetOrCreateEdgeTags(key, static k => [$"topic:{k.TopicOrRoutingKey}", "type:rabbitmq"]);
        var second = dsm.GetOrCreateEdgeTags(key, static k => [$"topic:{k.TopicOrRoutingKey}", "type:rabbitmq"]);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOrCreateEdgeTags_IbmMq_ReturnsSameArrayReference_WhenCalledTwiceWithSameKey()
    {
        var dsm = GetDataStreamManager(true, out _);
        var produceKey = new IbmMqEdgeTagCacheKey("queue1", IsConsume: false);
        var consumeKey = new IbmMqEdgeTagCacheKey("queue1", IsConsume: true);

        var produce1 = dsm.GetOrCreateEdgeTags(produceKey, static k => ["direction:out", $"topic:{k.QueueName}", "type:ibmmq"]);
        var produce2 = dsm.GetOrCreateEdgeTags(produceKey, static k => ["direction:out", $"topic:{k.QueueName}", "type:ibmmq"]);
        var consume1 = dsm.GetOrCreateEdgeTags(consumeKey, static k => ["direction:in", $"topic:{k.QueueName}", "type:ibmmq"]);
        var consume2 = dsm.GetOrCreateEdgeTags(consumeKey, static k => ["direction:in", $"topic:{k.QueueName}", "type:ibmmq"]);

        produce2.Should().BeSameAs(produce1);
        consume2.Should().BeSameAs(consume1);
        consume1.Should().NotBeSameAs(produce1); // same queue but different direction → different entry
    }

    [Fact]
    public void GetOrCreateEdgeTags_Kinesis_ReturnsSameArrayReference_WhenCalledTwiceWithSameKey()
    {
        var dsm = GetDataStreamManager(true, out _);
        var produceKey = new KinesisEdgeTagCacheKey("stream1", IsConsume: false);
        var consumeKey = new KinesisEdgeTagCacheKey("stream1", IsConsume: true);

        var produce1 = dsm.GetOrCreateEdgeTags(produceKey, static k => ["direction:out", $"topic:{k.StreamName}", "type:kinesis"]);
        var produce2 = dsm.GetOrCreateEdgeTags(produceKey, static k => ["direction:out", $"topic:{k.StreamName}", "type:kinesis"]);
        var consume1 = dsm.GetOrCreateEdgeTags(consumeKey, static k => ["direction:in", $"topic:{k.StreamName}", "type:kinesis"]);
        var consume2 = dsm.GetOrCreateEdgeTags(consumeKey, static k => ["direction:in", $"topic:{k.StreamName}", "type:kinesis"]);

        produce2.Should().BeSameAs(produce1);
        consume2.Should().BeSameAs(consume1);
        consume1.Should().NotBeSameAs(produce1);
    }

    [Fact]
    public void GetOrCreateEdgeTags_Sns_ReturnsSameArrayReference_WhenCalledTwiceWithSameKey()
    {
        var dsm = GetDataStreamManager(true, out _);
        var key = new SnsEdgeTagCacheKey("arn:topic1");

        var first = dsm.GetOrCreateEdgeTags(key, static k => ["direction:out", $"topic:{k.TopicName}", "type:sns"]);
        var second = dsm.GetOrCreateEdgeTags(key, static k => ["direction:out", $"topic:{k.TopicName}", "type:sns"]);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOrCreateEdgeTags_Sqs_ReturnsSameArrayReference_WhenCalledTwiceWithSameKey()
    {
        var dsm = GetDataStreamManager(true, out _);
        var produceKey = new SqsEdgeTagCacheKey("queue1", IsConsume: false);
        var consumeKey = new SqsEdgeTagCacheKey("queue1", IsConsume: true);

        var produce1 = dsm.GetOrCreateEdgeTags(produceKey, static k => ["direction:out", $"topic:{k.QueueName}", "type:sqs"]);
        var produce2 = dsm.GetOrCreateEdgeTags(produceKey, static k => ["direction:out", $"topic:{k.QueueName}", "type:sqs"]);
        var consume1 = dsm.GetOrCreateEdgeTags(consumeKey, static k => ["direction:in", $"topic:{k.QueueName}", "type:sqs"]);
        var consume2 = dsm.GetOrCreateEdgeTags(consumeKey, static k => ["direction:in", $"topic:{k.QueueName}", "type:sqs"]);

        produce2.Should().BeSameAs(produce1);
        consume2.Should().BeSameAs(consume1);
        consume1.Should().NotBeSameAs(produce1);
    }

    [Fact]
    public void GetOrCreateEdgeTags_ServiceBus_ReturnsSameArrayReference_WhenCalledTwiceWithSameKey()
    {
        var dsm = GetDataStreamManager(true, out _);
        var key = new ServiceBusEdgeTagCacheKey("my-entity");

        var first = dsm.GetOrCreateEdgeTags(key, static k => ["direction:in", $"topic:{k.EntityPath}", "type:servicebus"]);
        var second = dsm.GetOrCreateEdgeTags(key, static k => ["direction:in", $"topic:{k.EntityPath}", "type:servicebus"]);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOrCreateEdgeTags_BypassesCache_WhenAtMaxCapacity()
    {
        var dsm = GetDataStreamManager(true, out _);

        // Fill the per-type cache to the cap using a key type private to this test
        // (OverflowTestKey has its own static dictionary, isolated from ProduceEdgeTagCacheKey)
        for (var i = 0; i < DataStreamsManager.MaxEdgeTagCacheSize; i++)
        {
            dsm.GetOrCreateEdgeTags(new OverflowTestKey(i), static k => [$"tag:{k.Value}"]);
        }

        // The cache is now full; a new key should bypass caching and return a fresh array each call
        var overflowKey = new OverflowTestKey(DataStreamsManager.MaxEdgeTagCacheSize);
        var first = dsm.GetOrCreateEdgeTags(overflowKey, static k => [$"tag:{k.Value}"]);
        var second = dsm.GetOrCreateEdgeTags(overflowKey, static k => [$"tag:{k.Value}"]);

        second.Should().NotBeSameAs(first);
    }

    private static DataStreamsManager GetDataStreamManager(bool enabled, out DataStreamsWriterMock writer)
    {
        writer = enabled ? new DataStreamsWriterMock() : null;
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.Environment, "foo" },
                { ConfigurationKeys.ServiceName, "bar" },
                { ConfigurationKeys.DataStreamsMonitoring.Enabled, enabled.ToString() },
                // TODO: inject a deterministic value for process tags instead, to make test closer to reality
                // there are already tests about process tags, so this one is not required to "prove" it works
                // but it'd be cleaner not to have exclusions like this
                { ConfigurationKeys.PropagateProcessTags, "false" }
            });
        return new DataStreamsManager(settings, writer, Mock.Of<IDiscoveryService>());
    }

    /// <summary>
    /// Private key type used exclusively by <see cref="GetOrCreateEdgeTags_BypassesCache_WhenAtMaxCapacity"/>.
    /// Having a unique type gives an isolated <c>EdgeTagCache&lt;OverflowTestKey&gt;</c> dictionary.
    /// </summary>
    private readonly struct OverflowTestKey : IEquatable<OverflowTestKey>
    {
        public readonly int Value;

        public OverflowTestKey(int value) => Value = value;

        public bool Equals(OverflowTestKey other) => Value == other.Value;

        public override bool Equals(object obj) => obj is OverflowTestKey other && Equals(other);

        public override int GetHashCode() => Value;
    }

    internal class DataStreamsWriterMock : IDataStreamsWriter
    {
        private int _disposeCount;

        public ConcurrentQueue<StatsPoint> Points { get; } = new();

        public ConcurrentQueue<BacklogPoint> BacklogPoints { get; } = new();

        public DataStreamsTransactionContainer DataStreamsTransactions { get; } = new(1024);

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public void Add(in StatsPoint point)
        {
            Points.Enqueue(point);
        }

        public void AddTransaction(in DataStreamsTransactionInfo transaction)
        {
            DataStreamsTransactions.Add(transaction);
        }

        public void AddBacklog(in BacklogPoint point)
        {
            BacklogPoints.Enqueue(point);
        }

        public async Task DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            await Task.Yield();
        }

        public async Task FlushAsync()
        {
            await Task.Yield();
        }
    }
}
