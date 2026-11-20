// <copyright file="RabbitMQIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.RabbitMQ;

public class RabbitMQIntegrationTests
{
    private const long TimeInQueueMs = 30_000;

    // AMQP timestamps have one-second resolution, and CI machines can be slow.
    private const long ToleranceMs = 10_000;

    [Fact]
    public void GetTimeInQueueMs_NoTimestamp_ReturnsZero()
    {
        RabbitMQIntegration.GetTimeInQueueMs(0).Should().Be(0);
    }

    [Fact]
    public void GetTimeInQueueMs_TimestampInSeconds_ReturnsMilliseconds()
    {
        var publishedAt = DateTimeOffset.UtcNow.AddMilliseconds(-TimeInQueueMs).ToUnixTimeSeconds();

        RabbitMQIntegration.GetTimeInQueueMs(publishedAt).Should().BeInRange(TimeInQueueMs, TimeInQueueMs + ToleranceMs);
    }

    [Fact]
    public void GetTimeInQueueMs_TimestampInMilliseconds_ReturnsMilliseconds()
    {
        var publishedAt = DateTimeOffset.UtcNow.AddMilliseconds(-TimeInQueueMs).ToUnixTimeMilliseconds();

        RabbitMQIntegration.GetTimeInQueueMs(publishedAt).Should().BeInRange(TimeInQueueMs, TimeInQueueMs + ToleranceMs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetTimeInQueueMs_TimestampInFuture_ReturnsZero(bool inSeconds)
    {
        var future = DateTimeOffset.UtcNow.AddHours(1);
        var publishedAt = inSeconds ? future.ToUnixTimeSeconds() : future.ToUnixTimeMilliseconds();

        RabbitMQIntegration.GetTimeInQueueMs(publishedAt).Should().Be(0);
    }
}
