using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class SpanBufferTests
    {
        [Fact]
        public void SerializeSpans()
        {
            var formatterResolver = SpanFormatterResolver.Instance;

            var buffer = new SpanBuffer(10 * 1024 * 1024, formatterResolver);

            var traces = new List<Span[]>();

            for (int i = 0; i < 5; i++)
            {
                var spans = new Span[1];

                spans[0] = new Span(new SpanContext((ulong)i, (ulong)i), DateTimeOffset.UtcNow);

                traces.Add(spans);
            }

            var temporaryBuffer = new byte[1024];

            foreach (var trace in traces)
            {
                Assert.True(buffer.TryWrite(trace, ref temporaryBuffer));
            }

            buffer.Lock();

            var content = buffer.Data;

            var result = MessagePack.MessagePackSerializer.Deserialize<FakeSpan[][]>(content);

            Assert.Equal(5, result.Length);
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
