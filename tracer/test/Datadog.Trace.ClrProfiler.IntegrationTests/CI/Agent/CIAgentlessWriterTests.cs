// <copyright file="CIAgentlessWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Vendors.MessagePack;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI.Agent
{
    public class CIAgentlessWriterTests
    {
        [Fact]
        public async Task AgentlessTestEventTest()
        {
            var sender = new Mock<ICIAgentlessWriterSender>();
            var agentlessWriter = new CIAgentlessWriter(sender.Object);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            span.Type = SpanTypes.Test;
            span.SetTag(TestTags.Type, TestTags.TypeTest);

            var expectedPayload = new Ci.Agent.Payloads.CITestCyclePayload();
            expectedPayload.TryProcessEvent(new TestEvent(span));
            var expectedBytes = expectedPayload.ToArray();

            byte[] finalPayload = null;
            sender.Setup(x => x.SendPayloadAsync(It.IsAny<Ci.Agent.Payloads.EventsPayload>()))
                .Returns<Ci.Agent.Payloads.EventsPayload>(payload =>
                {
                    finalPayload = payload.ToArray();
                    return Task.CompletedTask;
                });

            var trace = new[] { span };
            agentlessWriter.WriteTrace(new ArraySegment<Span>(trace));
            await agentlessWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            Assert.True(finalPayload.SequenceEqual(expectedBytes));
        }

        [Fact]
        public async Task SlowSenderTest()
        {
            var flushTcs = new TaskCompletionSource<bool>();

            var sender = new Mock<ICIAgentlessWriterSender>();
            var agentlessWriter = new CIAgentlessWriter(sender.Object);
            var lstPayloads = new List<byte[]>();

            sender.Setup(x => x.SendPayloadAsync(It.IsAny<Ci.Agent.Payloads.EventsPayload>()))
                .Returns<Ci.Agent.Payloads.EventsPayload>(payload =>
                {
                    lstPayloads.Add(payload.ToArray());
                    return flushTcs.Task;
                });

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            var expectedPayload = new Ci.Agent.Payloads.CITestCyclePayload();
            expectedPayload.TryProcessEvent(new SpanEvent(span));
            expectedPayload.TryProcessEvent(new SpanEvent(span));
            expectedPayload.TryProcessEvent(new SpanEvent(span));
            var expectedBytes = expectedPayload.ToArray();

            agentlessWriter.WriteEvent(new SpanEvent(span));
            agentlessWriter.WriteEvent(new SpanEvent(span));
            agentlessWriter.WriteEvent(new SpanEvent(span));

            var firstFlush = agentlessWriter.FlushTracesAsync();

            agentlessWriter.WriteEvent(new SpanEvent(span));
            agentlessWriter.WriteEvent(new SpanEvent(span));
            agentlessWriter.WriteEvent(new SpanEvent(span));

            var secondFlush = agentlessWriter.FlushTracesAsync();
            flushTcs.TrySetResult(true);

            agentlessWriter.WriteEvent(new SpanEvent(span));
            agentlessWriter.WriteEvent(new SpanEvent(span));
            agentlessWriter.WriteEvent(new SpanEvent(span));

            var thirdFlush = agentlessWriter.FlushTracesAsync();

            await Task.WhenAll(firstFlush, secondFlush, thirdFlush);

            // We expect 3 batches.
            Assert.Equal(3, lstPayloads.Count);

            foreach (var payloadBytes in lstPayloads)
            {
                Assert.True(payloadBytes.SequenceEqual(expectedBytes));
            }
        }

        [Fact]
        public void EventsBufferTest()
        {
            int headerSize = Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>.HeaderSize;

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            var spanEvent = new SpanEvent(span);
            var individualType = MessagePackSerializer.Serialize<Ci.IEvent>(spanEvent, Ci.Agent.MessagePack.CIFormatterResolver.Instance);

            int bufferSize = 256;
            int maxBufferSize = (int)(4.5 * 1024 * 1024);

            while (bufferSize < maxBufferSize)
            {
                var eventBuffer = new Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>(bufferSize, Ci.Agent.MessagePack.CIFormatterResolver.Instance);
                while (eventBuffer.TryWrite(spanEvent))
                {
                    // .
                }

                // The number of items in the events should be the same as the num calculated
                // without decimals (items that doesn't fit doesn't get added)
                var numItemsTrunc = (bufferSize - headerSize) / individualType.Length;
                Assert.Equal(numItemsTrunc, eventBuffer.Count);

                bufferSize *= 2;
            }
        }
    }
}
