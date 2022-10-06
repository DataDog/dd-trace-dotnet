// <copyright file="SpanBufferTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using MessagePack; // use nuget MessagePack to deserialize
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class SpanBufferTests
    {
        private byte[] _temporaryBuffer = new byte[1024];

        [Theory]
        [InlineData(5, 5, false)]
        [InlineData(50, 50, true)]
        public void SerializeSpans(int traceCount, int spanCount, bool resizeExpected)
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, SpanFormatterResolver.Instance);

            for (int i = 0; i < traceCount; i++)
            {
                var spans = CreateTraceChunk(spanCount);
                buffer.TryWrite(spans, ref _temporaryBuffer).Should().BeTrue();
            }

            buffer.Lock();

            buffer.TraceCount.Should().Be(traceCount);
            buffer.SpanCount.Should().Be(traceCount * spanCount);

            var content = buffer.Data;
            var mockTraceChunks = MessagePackSerializer.Deserialize<MockSpan[][]>(content);
            var resized = content.Count > SpanBuffer.InitialBufferSize;

            // We want to test the case where the buffer is big enough from the start, and the case where it has to be resized
            // Make sure that the span/trace count assumptions are correct to test the scenario
            resized.Should().Be(resizeExpected, "Total serialized size was {0}", content.Count);

            mockTraceChunks.Length.Should().Be(traceCount);
            mockTraceChunks.Sum(t => t.Length).Should().Be(traceCount * spanCount);
        }

        [Fact]
        public void Overflow()
        {
            var buffer = new SpanBuffer(10, SpanFormatterResolver.Instance);

            buffer.IsFull.Should().BeFalse();

            var spans = CreateTraceChunk(1);
            var result = buffer.TryWrite(spans, ref _temporaryBuffer);

            result.Should().BeFalse();
            buffer.TraceCount.Should().Be(0);
            buffer.IsFull.Should().BeTrue();

            buffer.Lock();
            var innerBuffer = buffer.Data;

            innerBuffer.Array!.Skip(SpanBuffer.HeaderSize).All(b => b == 0x0).Should().BeTrue("No data should have been written to the buffer");

            buffer.Clear();

            buffer.IsFull.Should().BeFalse();
        }

        [Fact]
        public void LockingBuffer()
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, SpanFormatterResolver.Instance);
            var spans = CreateTraceChunk(1);

            buffer.TryWrite(spans, ref _temporaryBuffer).Should().BeTrue();
            buffer.Lock();
            buffer.TryWrite(spans, ref _temporaryBuffer).Should().BeFalse();
            buffer.Clear();
            buffer.TryWrite(spans, ref _temporaryBuffer).Should().BeTrue();
        }

        [Fact]
        public void ClearingBuffer()
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, SpanFormatterResolver.Instance);
            var spans = CreateTraceChunk(3);

            buffer.TryWrite(spans, ref _temporaryBuffer).Should().BeTrue();
            buffer.TraceCount.Should().Be(1);
            buffer.SpanCount.Should().Be(3);

            buffer.Clear();

            buffer.TraceCount.Should().Be(0);
            buffer.SpanCount.Should().Be(0);

            buffer.Lock();

            buffer.Data.Count.Should().Be(SpanBuffer.HeaderSize);
        }

        [Fact]
        public void InvalidSize()
        {
            Assert.Throws<ArgumentException>(() => new SpanBuffer(4, SpanFormatterResolver.Instance));
        }

        private static ArraySegment<Span> CreateTraceChunk(int spanCount, ulong startingId = 1)
        {
            var spans = new Span[spanCount];

            for (ulong i = 0; i < (ulong)spanCount; i++)
            {
                var spanContext = new SpanContext(startingId + i, startingId + i);
                spans[i] = new Span(spanContext, DateTimeOffset.UtcNow);
            }

            return new ArraySegment<Span>(spans);
        }
    }
}
