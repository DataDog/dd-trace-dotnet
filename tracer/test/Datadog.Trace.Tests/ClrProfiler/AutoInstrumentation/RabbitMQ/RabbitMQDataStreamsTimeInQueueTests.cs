// <copyright file="RabbitMQDataStreamsTimeInQueueTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// A consumed message with no upstream DSM context starts a new pathway, and the AMQP
    /// timestamp property is used to back-date its edge start to when the message was published.
    /// </summary>
    [TracerRestorer]
    public class RabbitMQDataStreamsTimeInQueueTests
    {
        private static readonly TimeSpan TimeInQueue = TimeSpan.FromSeconds(30);

        // The AMQP timestamp has one-second resolution, and CI machines can be slow.
        private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(10);

        public enum TimestampUnit
        {
            /// <summary>AMQP 0-9-1 and RabbitMQ.Client define the timestamp as POSIX seconds.</summary>
            Seconds,

            /// <summary>Some publishers write milliseconds into the field anyway.</summary>
            Milliseconds,
        }

        [Theory]
        [InlineData(TimestampUnit.Seconds)]
        [InlineData(TimestampUnit.Milliseconds)]
        public async Task BasicDeliver_EdgeStartIsMessagePublishTime(TimestampUnit unit)
        {
            var writer = new CapturingAgentWriter();
            await using var tracer = CreateDsmTracer(writer);
            var publishedAt = DateTimeOffset.UtcNow - TimeInQueue;
            var properties = new FakeBasicProperties(ToAmqpUnixTime(publishedAt, unit)).DuckCast<IReadOnlyBasicProperties>();
            var body = new FakeBody().DuckCast<IBody>();

            // TBody is constrained to IDuckType, so only the runtime duck proxy type can be the type argument.
            var state = (CallTargetState)typeof(RabbitMQIntegration)
                                        .GetMethod(nameof(RabbitMQIntegration.BasicDeliver_OnMethodBegin), BindingFlags.Static | BindingFlags.NonPublic)!
                                        .MakeGenericMethod(typeof(object), properties.GetType(), body.GetType())
                                        .Invoke(null, [new object(), false, "exchange", "routing-key", properties, body])!;

            var scope = state.Scope;
            scope.Should().NotBeNull();
            scope!.Dispose();

            AssertEdgeStartIsPublishTime(writer.SingleSpan("basic.deliver"), publishedAt);
        }

        [Theory]
        [InlineData(TimestampUnit.Seconds)]
        [InlineData(TimestampUnit.Milliseconds)]
        public async Task BasicGet_EdgeStartIsMessagePublishTime(TimestampUnit unit)
        {
            var writer = new CapturingAgentWriter();
            await using var tracer = CreateDsmTracer(writer);
            var publishedAt = DateTimeOffset.UtcNow - TimeInQueue;
            var result = new FakeBasicGetResult(new FakeBasicProperties(ToAmqpUnixTime(publishedAt, unit)), new FakeBody())
               .DuckCast<IBasicGetResult>();

            var instance = new object();
            var state = BasicGetIntegration.OnMethodBegin(instance, "queue", autoAck: true);
            // OnMethodEnd constrains TResult to IDuckType and returns a ref struct, so call it through a generic shim.
            typeof(RabbitMQDataStreamsTimeInQueueTests)
               .GetMethod(nameof(InvokeBasicGetOnMethodEnd), BindingFlags.Static | BindingFlags.NonPublic)!
               .MakeGenericMethod(result.GetType())
               .Invoke(null, [instance, result, state]);

            AssertEdgeStartIsPublishTime(writer.SingleSpan("basic.get"), publishedAt);
        }

        private static void InvokeBasicGetOnMethodEnd<TResult>(object instance, TResult result, CallTargetState state)
            where TResult : IBasicGetResult, IDuckType
            => BasicGetIntegration.OnMethodEnd(instance, result, exception: null, in state);

        private static long ToAmqpUnixTime(DateTimeOffset value, TimestampUnit unit)
            => unit == TimestampUnit.Seconds ? value.ToUnixTimeSeconds() : value.ToUnixTimeMilliseconds();

        private static void AssertEdgeStartIsPublishTime(Span span, DateTimeOffset publishedAt)
        {
            var pathway = span.Context.PathwayContext;
            pathway.Should().NotBeNull("DSM is enabled, so the consume checkpoint sets a pathway on the span");

            var edgeStart = DateTimeOffset.FromUnixTimeMilliseconds(pathway!.Value.EdgeStart / 1_000_000);
            var pathwayStart = DateTimeOffset.FromUnixTimeMilliseconds(pathway.Value.PathwayStart / 1_000_000);

            edgeStart.Should().BeCloseTo(publishedAt, Tolerance, "the message was published {0} before it was consumed", TimeInQueue);
            pathwayStart.Should().BeCloseTo(publishedAt, Tolerance, "a pathway that starts at this consume starts when the message was published");
        }

        private static ScopedTracer CreateDsmTracer(IAgentWriter writer)
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.DataStreamsMonitoring.Enabled, true } });
            var tracer = TracerHelper.Create(settings, agentWriter: writer);
            TracerRestorerAttribute.SetTracer(tracer);
            return tracer;
        }

        // Same member shape as RabbitMQ.Client's AmqpTimestamp
        public readonly struct FakeAmqpTimestamp
        {
            public readonly long UnixTime;

            public FakeAmqpTimestamp(long unixTime) => UnixTime = unixTime;
        }

        // Same member shape as the parts of RabbitMQ.Client's IBasicProperties the integration reads
        public class FakeBasicProperties
        {
            public FakeBasicProperties(long unixTime) => Timestamp = new FakeAmqpTimestamp(unixTime);

            // Headers without DSM context, as sent by an uninstrumented producer
            public IDictionary<string, object> Headers { get; } = new Dictionary<string, object>();

            public byte DeliveryMode => 2;

            public FakeAmqpTimestamp Timestamp { get; }

            public bool IsDeliveryModePresent() => true;
        }

        // Same member shape as the parts of RabbitMQ.Client's BasicGetResult the integration reads
        public class FakeBasicGetResult
        {
            public FakeBasicGetResult(FakeBasicProperties basicProperties, FakeBody body)
            {
                BasicProperties = basicProperties;
                Body = body;
            }

            public FakeBasicProperties BasicProperties { get; }

            public FakeBody Body { get; }
        }

        // Stands in for RabbitMQ.Client's ReadOnlyMemory<byte> body; ReadOnlyMemory<T> is ambiguous here on
        // netcoreapp2.1 and .NET Framework because Datadog.Trace vendors System.Memory.
        public class FakeBody
        {
            public int Length => 10;
        }

        private class CapturingAgentWriter : IAgentWriter
        {
            private readonly ConcurrentQueue<Span> _spans = new();

            public void WriteTrace(in SpanCollection trace)
            {
                foreach (var span in trace)
                {
                    _spans.Enqueue(span);
                }
            }

            public Span SingleSpan(string resourcePrefix)
                => _spans.Should().ContainSingle(s => s.ResourceName != null && s.ResourceName.StartsWith(resourcePrefix)).Which;

            public Task<bool> Ping() => Task.FromResult(true);

            public Task FlushTracesAsync() => Task.CompletedTask;

            public Task FlushAndCloseAsync() => Task.CompletedTask;
        }
    }
}
