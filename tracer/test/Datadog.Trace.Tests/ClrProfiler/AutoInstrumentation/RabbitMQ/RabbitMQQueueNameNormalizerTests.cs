// <copyright file="RabbitMQQueueNameNormalizerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.RabbitMQ;

public class RabbitMQQueueNameNormalizerTests
{
    [Theory]
    // null / empty
    [InlineData(null, null)]
    [InlineData("", "")]
    // server-named queues
    [InlineData("amq.gen-JzTY20BRgKO-HjmUJj0wLg", "amq.gen-*")]
    [InlineData("amq.gen-", "amq.gen-*")]
    // direct reply-to
    [InlineData("amq.rabbitmq.reply-to", "amq.rabbitmq.reply-to")]
    [InlineData("amq.rabbitmq.reply-to.g1h2AA5yZXBseUAxMjcuMC4wLjE6NTY3Mg==", "amq.rabbitmq.reply-to")]
    // MassTransit bus / temporary endpoints
    [InlineData("ip-10-0-1-2_PaymentService_bus_ybyyyyb6tamq6nr4bdpwzyhwbo", "*_PaymentService_bus_*")]
    [InlineData("MY-HOST_OrderApi_BUS_YBYYYYB6TAMQ6NR4BDPWZYHWBO", "*_OrderApi_BUS_*")]
    [InlineData("my_host_name_Worker_bus_abc123", "*_Worker_bus_*")]
    // Wolverine node queues
    [InlineData("wolverine.control.3", "wolverine.control.*")]
    [InlineData("wolverine.response.8f14e45f-ceea-467a-9f3a-6b1f1e2c0a11", "wolverine.response.*")]
    [InlineData("wolverine.replies.node-1", "wolverine.replies.*")]
    // trailing UUID
    [InlineData("callback-8f14e45f-ceea-467a-9f3a-6b1f1e2c0a11", "callback-*")]
    [InlineData("reply-orders-8F14E45F-CEEA-467A-9F3A-6B1F1E2C0A11", "reply-orders-*")]
    [InlineData("reply_8f14e45fceea467a9f3a6b1f1e2c0a11", "reply_*")]
    [InlineData("events.8f14e45f-ceea-467a-9f3a-6b1f1e2c0a11", "events.*")]
    [InlineData("foo_8f14e45f-ceea-467a-9f3a-6b1f1e2c0a11", "foo_*")]
    // trailing long hex
    [InlineData("membership-9f8e7d6c5b4a39281706f5e4d3c2b1a0", "membership-*")]
    [InlineData("worker.0123456789abcdef", "worker.*")]
    // bare UUID
    [InlineData("8f14e45f-ceea-467a-9f3a-6b1f1e2c0a11", "*")]
    [InlineData("8F14E45F-CEEA-467A-9F3A-6B1F1E2C0A11", "*")]
    [InlineData("8f14e45fceea467a9f3a6b1f1e2c0a11", "*")]
    // unchanged
    [InlineData("orders", "orders")]
    [InlineData("orders-v2", "orders-v2")]
    [InlineData("queue-abc123", "queue-abc123")]
    [InlineData("worker.0123456789abcde", "worker.0123456789abcde")] // 15 hex chars
    [InlineData("worker0123456789abcdef", "worker0123456789abcdef")] // no separator
    [InlineData("0123456789abcdef0123", "0123456789abcdef0123")] // no separator
    [InlineData("8f14e45f-ceea-467a-9f3a-6b1f1e2c0a1", "8f14e45f-ceea-467a-9f3a-6b1f1e2c0a1")] // 35 chars, not a UUID
    [InlineData("8f14e45fceea467a9f3a6b1f1e2c0a1", "8f14e45fceea467a9f3a6b1f1e2c0a1")] // 31 hex
    [InlineData("0123456789abcdef01234567", "0123456789abcdef01234567")] // 24 hex
    [InlineData("test.topic.chocolate.cake", "test.topic.chocolate.cake")]
    [InlineData("amq.direct", "amq.direct")]
    [InlineData("PaymentService_bus", "PaymentService_bus")]
    [InlineData("PaymentService_bus_abc-123", "PaymentService_bus_abc-123")]
    [InlineData("wolverine.control.", "wolverine.control.")]
    [InlineData("wolverine.durable.3", "wolverine.durable.3")]
    [InlineData("callback-8f14e45f-ceea-467a-9f3a-6b1f1e2c0a1", "callback-8f14e45f-ceea-467a-9f3a-6b1f1e2c0a1")] // UUID too short
    public void Normalize(string? name, string? expected)
    {
        RabbitMQQueueNameNormalizer.Normalize(name).Should().Be(expected);
    }

    [Fact]
    public void Normalize_UnchangedName_ReturnsSameInstance()
    {
        var name = "orders-v2";
        RabbitMQQueueNameNormalizer.Normalize(name).Should().BeSameAs(name);
    }
}
