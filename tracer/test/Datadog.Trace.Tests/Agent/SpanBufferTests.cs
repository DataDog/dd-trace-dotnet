// <copyright file="SpanBufferTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.OpenTelemetry.Traces;
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
            var buffer = new SpanBuffer(10 * 1024 * 1024, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));

            for (int i = 0; i < traceCount; i++)
            {
                var spans = CreateTraceChunk(spanCount);
                buffer.TryWrite(spans, ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            }

            var payload = buffer.Detach(new byte[SpanBuffer.InitialBufferSize]);

            payload.TraceCount.Should().Be(traceCount);
            payload.SpanCount.Should().Be(traceCount * spanCount);

            var content = payload.Data;
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
            var buffer = new SpanBuffer(10, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));

            buffer.IsFull.Should().BeFalse();

            var spans = CreateTraceChunk(1);
            var result = buffer.TryWrite(spans, ref _temporaryBuffer);

            result.Should().Be(SpanBuffer.WriteStatus.Overflow);
            buffer.TraceCount.Should().Be(0);
            buffer.IsFull.Should().BeFalse();

            var innerBuffer = buffer.RawData;

            innerBuffer.Array!.Skip(SpanBufferMessagePackSerializer.HeaderSizeConst).All(b => b == 0x0).Should().BeTrue("No data should have been written to the buffer");
        }

        [Fact]
        public void DetachingBuffer_HandsOverThePayloadAndResetsTheBuffer()
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));

            buffer.TryWrite(CreateTraceChunk(3), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            buffer.TryWrite(CreateTraceChunk(3), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);

            var payload = buffer.Detach(new byte[SpanBuffer.InitialBufferSize]);

            payload.Array.Should().NotBeNull();
            payload.TraceCount.Should().Be(2);
            payload.SpanCount.Should().Be(6);

            var chunks = MessagePackSerializer.Deserialize<MockSpan[][]>(payload.Data);
            chunks.Length.Should().Be(2);
            chunks.Sum(t => t.Length).Should().Be(6);

            // The buffer handed everything over, so it's empty again
            buffer.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void DetachingBuffer_LeavesThePayloadIntactWhileTheBufferIsWrittenTo()
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));

            buffer.TryWrite(CreateTraceChunk(3), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);

            var payload = buffer.Detach(new byte[SpanBuffer.InitialBufferSize]);

            // The whole point of detaching: the buffer is writable straight away, and none of
            // those writes may touch the payload that is still being sent
            for (var i = 0; i < 10; i++)
            {
                buffer.TryWrite(CreateTraceChunk(5), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            }

            var chunks = MessagePackSerializer.Deserialize<MockSpan[][]>(payload.Data);
            chunks.Length.Should().Be(1);
            chunks[0].Length.Should().Be(3);
        }

        [Fact]
        public void DetachingBuffer_InstallsTheReplacementArray()
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));
            var replacement = new byte[SpanBuffer.InitialBufferSize];

            buffer.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            var first = buffer.Detach(replacement);

            buffer.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            var second = buffer.Detach(first.Array!);

            first.Array.Should().NotBeSameAs(replacement);
            second.Array.Should().BeSameAs(replacement, "the replacement handed to the first detach becomes the backing array");
        }

        [Fact]
        public void DetachingEmptyBuffer_ReturnsNothingAndDoesNotConsumeTheReplacement()
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));
            var replacement = new byte[SpanBuffer.InitialBufferSize];

            var payload = buffer.Detach(replacement);

            payload.Array.Should().BeNull();
            payload.TraceCount.Should().Be(0);
            payload.SpanCount.Should().Be(0);

            // The replacement wasn't taken, so the caller can still use it for the next flush
            buffer.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            buffer.Detach(replacement).Array.Should().NotBeSameAs(replacement);
        }

        [Fact]
        public void TraceCount_TracksWhetherThereIsAnythingToFlush()
        {
            // The flush loop reads this without taking the buffer's lock, to decide whether a
            // buffer is worth sizing a replacement array for.
            var buffer = new SpanBuffer(10 * 1024 * 1024, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));

            buffer.TraceCount.Should().Be(0);

            buffer.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            buffer.TraceCount.Should().Be(1);

            buffer.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            buffer.TraceCount.Should().Be(2);

            buffer.Detach(new byte[SpanBuffer.InitialBufferSize]).Array.Should().NotBeNull();
            buffer.TraceCount.Should().Be(0);
        }

        [Fact]
        public void DetachingFullBuffer_ClearsTheFullFlag()
        {
            var buffer = new SpanBuffer(512, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));

            SpanBuffer.WriteStatus status;

            do
            {
                status = buffer.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer);
            }
            while (status == SpanBuffer.WriteStatus.Success);

            status.Should().Be(SpanBuffer.WriteStatus.Full);
            buffer.IsFull.Should().BeTrue();

            buffer.Detach(new byte[512]);

            buffer.IsFull.Should().BeFalse();
            buffer.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
        }

        [Fact]
        public void JsonSerializer_CanAlwaysCloseThePayload_EvenWhenTheBufferFillsUp()
        {
            // FinishBody appends the closing brackets when the payload is detached, and that runs
            // while the buffer's lock is held. Every write reserves TrailerSize bytes for them, so
            // finalizing can never need to grow the array there -- and the payload can never come
            // out unterminated.
            var serializer = new OtlpTracesJsonSerializer();

            // Measure two chunks, then size the real buffer so that without the reservation it
            // would fill to within fewer bytes than the closing brackets need. That is the case
            // where finalizing used to silently give up and emit unterminated JSON.
            var probe = new SpanBuffer(64 * 1024, new OtlpTracesJsonSerializer());
            probe.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
            probe.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);

            var buffer = new SpanBuffer(probe.RawData.Count + serializer.TrailerSize - 1, serializer);

            SpanBuffer.WriteStatus status;

            do
            {
                status = buffer.TryWrite(CreateTraceChunk(1), ref _temporaryBuffer);
            }
            while (status == SpanBuffer.WriteStatus.Success);

            status.Should().Be(SpanBuffer.WriteStatus.Full, "the buffer should fill up rather than reject the first chunk");

            var payload = buffer.Detach(new byte[64 * 1024]);

            payload.Array.Should().NotBeNull();

            var json = Encoding.UTF8.GetString(payload.Data.Array!, payload.Data.Offset, payload.Data.Count);
            json.Should().EndWith("]}]}]}", "the closing brackets must always fit");
        }

        [Fact]
        public void InvalidSize()
        {
            Assert.Throws<ArgumentException>(() => new SpanBuffer(4, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance)));
        }

        [Fact]
        public void TemporaryBufferSizeLimit()
        {
            var buffer = new SpanBuffer(256, new SpanBufferMessagePackSerializer(SpanFormatterResolver.Instance));
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

            var buffer = new SpanBuffer(maxBufferSize: 256, new SpanBufferMessagePackSerializer(mockResolver.Object));
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
