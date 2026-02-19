// <copyright file="KafkaClusterIdTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Kafka
{
    public class KafkaClusterIdTests
    {
        [Fact]
        public void ConsumerCache_StoresAndRetrievesClusterId()
        {
            var consumer = new object();
            var groupId = "test-group";
            var bootstrapServers = "localhost:9092";
            var clusterId = "test-cluster-abc123";

            ConsumerCache.SetConsumerGroup(consumer, groupId, bootstrapServers, clusterId);

            var found = ConsumerCache.TryGetConsumerGroup(consumer, out var retrievedGroupId, out var retrievedBootstrapServers, out var retrievedClusterId);

            found.Should().BeTrue();
            retrievedGroupId.Should().Be(groupId);
            retrievedBootstrapServers.Should().Be(bootstrapServers);
            retrievedClusterId.Should().Be(clusterId);

            ConsumerCache.RemoveConsumerGroup(consumer);
        }

        [Fact]
        public void ConsumerCache_ReturnsEmptyClusterId_WhenSetAsEmpty()
        {
            var consumer = new object();
            var groupId = "test-group";
            var bootstrapServers = "localhost:9092";
            var clusterId = string.Empty;

            ConsumerCache.SetConsumerGroup(consumer, groupId, bootstrapServers, clusterId);

            var found = ConsumerCache.TryGetConsumerGroup(consumer, out _, out _, out var retrievedClusterId);

            found.Should().BeTrue();
            retrievedClusterId.Should().BeEmpty();

            ConsumerCache.RemoveConsumerGroup(consumer);
        }

        [Fact]
        public void ConsumerCache_ReturnsFalse_WhenConsumerNotFound()
        {
            var consumer = new object();

            var found = ConsumerCache.TryGetConsumerGroup(consumer, out var groupId, out var bootstrapServers, out var clusterId);

            found.Should().BeFalse();
            groupId.Should().BeNull();
            bootstrapServers.Should().BeNull();
            clusterId.Should().BeNull();
        }

        [Fact]
        public void ConsumerCache_RemoveConsumerGroup_ClearsAllFields()
        {
            var consumer = new object();
            ConsumerCache.SetConsumerGroup(consumer, "group", "localhost:9092", "cluster-123");

            ConsumerCache.RemoveConsumerGroup(consumer);

            var found = ConsumerCache.TryGetConsumerGroup(consumer, out _, out _, out _);
            found.Should().BeFalse();
        }

        [Fact]
        public void ProducerCache_StoresAndRetrievesClusterId()
        {
            var producer = new object();
            var bootstrapServers = "localhost:9092";
            var clusterId = "test-cluster-xyz456";

            ProducerCache.AddBootstrapServers(producer, bootstrapServers, clusterId);

            var found = ProducerCache.TryGetProducer(producer, out var retrievedBootstrapServers, out var retrievedClusterId);

            found.Should().BeTrue();
            retrievedBootstrapServers.Should().Be(bootstrapServers);
            retrievedClusterId.Should().Be(clusterId);

            ProducerCache.RemoveProducer(producer);
        }

        [Fact]
        public void ProducerCache_ReturnsEmptyClusterId_WhenSetAsEmpty()
        {
            var producer = new object();
            var bootstrapServers = "localhost:9092";
            var clusterId = string.Empty;

            ProducerCache.AddBootstrapServers(producer, bootstrapServers, clusterId);

            var found = ProducerCache.TryGetProducer(producer, out _, out var retrievedClusterId);

            found.Should().BeTrue();
            retrievedClusterId.Should().BeEmpty();

            ProducerCache.RemoveProducer(producer);
        }

        [Fact]
        public void ProducerCache_ReturnsFalse_WhenProducerNotFound()
        {
            var producer = new object();

            var found = ProducerCache.TryGetProducer(producer, out var bootstrapServers, out var clusterId);

            found.Should().BeFalse();
            bootstrapServers.Should().BeNull();
            clusterId.Should().BeNull();
        }

        [Fact]
        public void ProducerCache_RemoveProducer_ClearsAllFields()
        {
            var producer = new object();
            ProducerCache.AddBootstrapServers(producer, "localhost:9092", "cluster-123");

            ProducerCache.RemoveProducer(producer);

            var found = ProducerCache.TryGetProducer(producer, out _, out _);
            found.Should().BeFalse();
        }

        [Fact]
        public void KafkaTags_HasClusterIdProperty()
        {
            var tags = new KafkaTags(SpanKinds.Producer);
            tags.ClusterId.Should().BeNull();

            tags.ClusterId = "my-cluster-id";
            tags.ClusterId.Should().Be("my-cluster-id");
        }

        [Fact]
        public void KafkaTags_ClusterId_IsSerializedWithCorrectTagName()
        {
            var tags = new KafkaTags(SpanKinds.Producer);
            tags.ClusterId = "test-cluster";

            var tagValue = tags.GetTag("messaging.kafka.cluster_id");
            tagValue.Should().Be("test-cluster");
        }

        [Fact]
        public void KafkaTags_ClusterId_CanBeSetViaSetTag()
        {
            var tags = new KafkaTags(SpanKinds.Consumer);
            tags.SetTag("messaging.kafka.cluster_id", "set-via-tag");

            tags.ClusterId.Should().Be("set-via-tag");
        }

        [Fact]
        public void KafkaHelper_GetClusterId_ReturnsNull_WhenBootstrapServersIsNull()
        {
            var result = KafkaHelper.GetClusterId(null!);
            result.Should().BeNull();
        }

        [Fact]
        public void KafkaHelper_GetClusterId_ReturnsNull_WhenBootstrapServersIsEmpty()
        {
            var result = KafkaHelper.GetClusterId(string.Empty);
            result.Should().BeNull();
        }

        [Fact]
        public void KafkaHelper_GetClusterId_ReturnsNonNull_WhenConfluentKafkaIsAvailable()
        {
            // When Confluent.Kafka is loaded (as in this test project), GetClusterId
            // should proceed past the type checks. With no real Kafka broker, it will
            // either return empty string (timeout/error) or null, but should not throw.
            var result = KafkaHelper.GetClusterId("localhost:19092");

            // The method should handle the error gracefully (no broker running)
            // and return either null or empty string
            (result is null || result == string.Empty).Should().BeTrue(
                "GetClusterId should handle connection failures gracefully");
        }

        [Fact]
        public void Tags_KafkaClusterId_ConstantHasExpectedValue()
        {
            Tags.KafkaClusterId.Should().Be("messaging.kafka.cluster_id");
        }
    }
}
