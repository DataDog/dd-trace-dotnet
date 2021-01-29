using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
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

            var traces = new List<Span[]>();

            for (int i = 0; i < traceCount; i++)
            {
                var spans = new Span[spanCount];

                for (int j = 0; j < spanCount; j++)
                {
                    spans[j] = new Span(new SpanContext((ulong)i, (ulong)i), DateTimeOffset.UtcNow);
                }

                traces.Add(spans);
            }

            foreach (var trace in traces)
            {
                Assert.True(buffer.TryWrite(trace, ref _temporaryBuffer));
            }

            buffer.Lock();

            Assert.Equal(traceCount, buffer.TraceCount);
            Assert.Equal(traceCount * spanCount, buffer.SpanCount);

            var content = buffer.Data;

            var result = MessagePack.MessagePackSerializer.Deserialize<FakeSpan[][]>(content);

            var resized = content.Count > SpanBuffer.InitialBufferSize;

            // We want to test the case where the buffer is big enough from the start, and the case where it has to be resized
            // Make sure that the span/trace count assumptions are correct to test the scenario
            Assert.True(resizeExpected == resized, $"Total serialized size was {content.Count}");

            Assert.Equal(traceCount, result.Length);
            Assert.Equal(traceCount * spanCount, result.Sum(t => t.Length));
        }

        [Fact]
        public void Overflow()
        {
            var buffer = new SpanBuffer(10, SpanFormatterResolver.Instance);

            Assert.False(buffer.IsFull);

            var trace = new[] { new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow) };

            var result = buffer.TryWrite(trace, ref _temporaryBuffer);

            Assert.False(result);
            Assert.Equal(0, buffer.TraceCount);
            Assert.True(buffer.IsFull);

            buffer.Lock();

            var innerBuffer = buffer.Data;

            Assert.True(innerBuffer.Array.Skip(SpanBuffer.HeaderSize).All(b => b == 0x0), "No data should have been written to the buffer");

            buffer.Clear();

            Assert.False(buffer.IsFull);
        }

        [Fact]
        public void LockingBuffer()
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, SpanFormatterResolver.Instance);

            var trace = new[] { new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow) };

            Assert.True(buffer.TryWrite(trace, ref _temporaryBuffer));

            buffer.Lock();

            Assert.False(buffer.TryWrite(trace, ref _temporaryBuffer));

            buffer.Clear();

            Assert.True(buffer.TryWrite(trace, ref _temporaryBuffer));
        }

        [Fact]
        public void ClearingBuffer()
        {
            var buffer = new SpanBuffer(10 * 1024 * 1024, SpanFormatterResolver.Instance);

            var trace = new[]
            {
                new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow),
                new Span(new SpanContext(2, 2), DateTimeOffset.UtcNow),
                new Span(new SpanContext(3, 3), DateTimeOffset.UtcNow),
            };

            Assert.True(buffer.TryWrite(trace, ref _temporaryBuffer));

            Assert.Equal(1, buffer.TraceCount);
            Assert.Equal(3, buffer.SpanCount);

            buffer.Clear();

            Assert.Equal(0, buffer.TraceCount);
            Assert.Equal(0, buffer.SpanCount);

            buffer.Lock();

            var innerBuffer = buffer.Data;
            Assert.Equal(SpanBuffer.HeaderSize, innerBuffer.Count);
        }

        [Fact]
        public void InvalidSize()
        {
            Assert.Throws<ArgumentException>(() => new SpanBuffer(4, SpanFormatterResolver.Instance));
        }

        [MessagePack.MessagePackObject]
        public struct FakeSpan
        {
            [MessagePack.Key("trace_id")]
            public ulong TraceId { get; set; }

            [MessagePack.Key("span_id")]
            public ulong SpanId { get; set; }

            [MessagePack.Key("name")]
            public string Name { get; set; }

            [MessagePack.Key("resource")]
            public string Resource { get; set; }

            [MessagePack.Key("service")]
            public string Service { get; set; }

            [MessagePack.Key("type")]
            public string Type { get; set; }

            [MessagePack.Key("start")]
            public long Start { get; set; }

            [MessagePack.Key("duration")]
            public long Duration { get; set; }

            [MessagePack.Key("parent_id")]
            public ulong? ParentId { get; set; }

            [MessagePack.Key("error")]
            public byte Error { get; set; }

            [MessagePack.Key("meta")]
            public Dictionary<string, string> Tags { get; set; }

            [MessagePack.Key("metrics")]
            public Dictionary<string, double> Metrics { get; set; }
        }
    }
}
