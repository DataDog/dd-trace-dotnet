// <copyright file="SpanBufferTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using FluentAssertions;
using MessagePack; // use nuget MessagePack to deserialize
using Moq;
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
                buffer.TryWrite(spans, ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
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

            result.Should().Be(SpanBuffer.WriteStatus.Full);
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

            buffer.TryWrite(spans, ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            buffer.Lock();
            buffer.TryWrite(spans, ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Full);
            buffer.Clear();
            buffer.TryWrite(spans, ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
        }

        [Fact]
        public void ClearingBuffer()
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, SpanFormatterResolver.Instance);
            var spans = CreateTraceChunk(3);

            buffer.TryWrite(spans, ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
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

        [Fact]
        public void TemporaryBufferSizeLimit()
        {
            var buffer = new SpanBuffer(256, SpanFormatterResolver.Instance);
            var temporaryBuffer = new byte[256];
            var spans = CreateTraceChunk(10);

            buffer.TryWrite(spans, ref temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Overflow);
            buffer.IsFull.Should().BeFalse();
            buffer.SpanCount.Should().Be(0);
            buffer.TraceCount.Should().Be(0);

            temporaryBuffer.Length.Should().BeLessThanOrEqualTo(512, because: "the size of the temporary buffer shouldn't exceed twice the limit");
        }

        [Fact]
        public void IsFirstChunkInBuffer_FirstChunkIsTrue_SubsequentChunksAreFalse()
        {
            var interceptedChunks = new List<TraceChunkModel>();
            var interceptingFormatter = new InterceptingTraceChunkFormatter(interceptedChunks);
            var mockResolver = new Mock<Vendors.MessagePack.IFormatterResolver>();
            mockResolver.Setup(r => r.GetFormatter<TraceChunkModel>()).Returns(interceptingFormatter);

            var buffer = new SpanBuffer(maxBufferSize: 256, mockResolver.Object);
            var temporaryBuffer = new byte[256];

            var firstSpanArray = CreateTraceChunk(2);
            var secondSpanArray = CreateTraceChunk(spanCount: 2, startingId: 10);

            buffer.TryWrite(firstSpanArray, ref temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            buffer.TryWrite(secondSpanArray, ref temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);

            interceptedChunks.Should().HaveCount(2);
            interceptedChunks[0].IsFirstChunkInPayload.Should().BeTrue();
            interceptedChunks[1].IsFirstChunkInPayload.Should().BeFalse();
        }

        private static SpanCollection CreateTraceChunk(int spanCount, ulong startingId = 1)
        {
            var spans = new Span[spanCount];

            for (ulong i = 0; i < (ulong)spanCount; i++)
            {
                var spanContext = new SpanContext(startingId + i, startingId + i);
                spans[i] = new Span(spanContext, DateTimeOffset.UtcNow);
            }

            return new SpanCollection(spans);
        }

        /// <summary>
        /// practical mock, because the presence of the ref modifier on bytes makes it not work well with Moq.
        /// </summary>
        private class InterceptingTraceChunkFormatter(List<TraceChunkModel> interceptedChunks) : IMessagePackFormatter<TraceChunkModel>
        {
            public int Serialize(ref byte[] bytes, int offset, TraceChunkModel value, Vendors.MessagePack.IFormatterResolver formatterResolver)
            {
                interceptedChunks.Add(value);
                return 50; // Return a reasonable serialized size
            }

            public TraceChunkModel Deserialize(byte[] bytes, int offset, Vendors.MessagePack.IFormatterResolver formatterResolver, out int readSize)
            {
                throw new NotImplementedException("Deserialization not needed for this test");
            }
        }
    }
}
