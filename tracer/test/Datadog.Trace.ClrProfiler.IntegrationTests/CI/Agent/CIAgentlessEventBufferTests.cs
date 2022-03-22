// <copyright file="CIAgentlessEventBufferTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Ci.EventModel;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI.Agent
{
    public class CIAgentlessEventBufferTests
    {
        [Theory]
        [InlineData(5, 5, false)]
        [InlineData(50, 50, true)]
        public void SerializeSpans(int traceCount, int spanCount, bool resizeExpected)
        {
            var buffer = new Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>(10 * 1024 * 1024, Ci.Agent.MessagePack.CIFormatterResolver.Instance);

            var events = new List<Ci.IEvent>();

            for (int i = 0; i < traceCount; i++)
            {
                for (int j = 0; j < spanCount; j++)
                {
                    events.Add(new SpanEvent(new Span(new SpanContext((ulong)i, (ulong)i), DateTimeOffset.UtcNow)));
                }
            }

            foreach (var @event in events)
            {
                Assert.True(buffer.TryWrite(@event));
            }

            buffer.Lock();

            Assert.Equal(traceCount * spanCount, buffer.Count);

            var content = buffer.Data;
            var resized = content.Count > Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>.InitialBufferSize;

            // We want to test the case where the buffer is big enough from the start, and the case where it has to be resized
            // Make sure that the span/trace count assumptions are correct to test the scenario
            Assert.True(resizeExpected == resized, $"Total serialized size was {content.Count}");
        }

        [Fact]
        public void Overflow()
        {
            var buffer = new Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>(10, Ci.Agent.MessagePack.CIFormatterResolver.Instance);

            Assert.False(buffer.IsFull);

            var spanEvent = new SpanEvent(new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow));

            var result = buffer.TryWrite(spanEvent);

            Assert.False(result);
            Assert.Equal(0, buffer.Count);
            Assert.True(buffer.IsFull);

            buffer.Lock();

            var innerBuffer = buffer.Data;

            var actualBuffer = innerBuffer.Skip(innerBuffer.Offset).Take(innerBuffer.Count).ToArray();

            Assert.True(actualBuffer.Skip(Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>.HeaderSize).All(b => b == 0x0), "No data should have been written to the buffer");

            buffer.Clear();

            Assert.False(buffer.IsFull);
        }

        [Fact]
        public void LockingBuffer()
        {
            var buffer = new Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>(10 * 1024 * 1024, Ci.Agent.MessagePack.CIFormatterResolver.Instance);
            var spanEvent = new SpanEvent(new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow));

            Assert.True(buffer.TryWrite(spanEvent));

            buffer.Lock();

            Assert.False(buffer.TryWrite(spanEvent));

            buffer.Clear();

            Assert.True(buffer.TryWrite(spanEvent));
        }

        [Fact]
        public void ClearingBuffer()
        {
            var buffer = new Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>(10 * 1024 * 1024, Ci.Agent.MessagePack.CIFormatterResolver.Instance);
            var spanEvent = new SpanEvent(new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow));

            Assert.True(buffer.TryWrite(spanEvent));

            Assert.Equal(1, buffer.Count);

            buffer.Clear();

            Assert.Equal(0, buffer.Count);

            buffer.Lock();

            var innerBuffer = buffer.Data;
            Assert.Equal(Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>.HeaderSize, innerBuffer.Count);
        }

        [Fact]
        public void InvalidSize()
        {
            Assert.Throws<ArgumentException>(() => new Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>(4, Ci.Agent.MessagePack.CIFormatterResolver.Instance));
        }
    }
}
