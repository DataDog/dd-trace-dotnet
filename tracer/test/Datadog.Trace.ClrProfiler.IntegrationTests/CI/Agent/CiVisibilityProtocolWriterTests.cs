// <copyright file="CiVisibilityProtocolWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI.Agent
{
    public class CiVisibilityProtocolWriterTests
    {
        [Fact]
        public async Task AgentlessTestEventTest()
        {
            var settings = CIVisibility.Settings;
            var sender = new Mock<ICIVisibilityProtocolWriterSender>();
            var agentlessWriter = new CIVisibilityProtocolWriter(settings, sender.Object);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow, new TestSpanTags());
            span.Type = SpanTypes.Test;
            span.SetTag(TestTags.Type, TestTags.TypeTest);

            var expectedPayload = new Ci.Agent.Payloads.CITestCyclePayload(settings);
            expectedPayload.TryProcessEvent(new TestEvent(span));
            var expectedBytes = expectedPayload.ToArray();

            byte[] finalPayload = null;
            sender.Setup(x => x.SendPayloadAsync(It.IsAny<Ci.Agent.Payloads.CIVisibilityProtocolPayload>()))
                .Returns<Ci.Agent.Payloads.CIVisibilityProtocolPayload>(payload =>
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
        public async Task AgentlessStreamTestEventTest()
        {
            var settings = CIVisibility.Settings;
            var sender = new Mock<ICIVisibilityProtocolWriterSender>();
            var agentlessWriter = new CIVisibilityProtocolWriter(settings, sender.Object);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow, new TestSpanTags());
            span.Type = SpanTypes.Test;
            span.SetTag(TestTags.Type, TestTags.TypeTest);

            var expectedPayload = new Ci.Agent.Payloads.CITestCyclePayload(settings);
            expectedPayload.TryProcessEvent(new TestEvent(span));
            var mStreamExpected = new MemoryStream();
            expectedPayload.WriteTo(mStreamExpected);
            var expectedBytes = mStreamExpected.ToArray();

            byte[] finalPayload = null;
            sender.Setup(x => x.SendPayloadAsync(It.IsAny<Ci.Agent.Payloads.CIVisibilityProtocolPayload>()))
                  .Returns<Ci.Agent.Payloads.CIVisibilityProtocolPayload>(payload =>
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
        public async Task AgentlessCodeCoverageEvent()
        {
            var settings = CIVisibility.Settings;
            var sender = new Mock<ICIVisibilityProtocolWriterSender>();
            var agentlessWriter = new CIVisibilityProtocolWriter(settings, sender.Object);
            var coveragePayload = new TestCoverage
            {
                SessionId = 42,
                SuiteId = 56,
                SpanId = 84,
                Files =
                [
                    new FileCoverage
                    {
                        FileName = "MyFile",
                        Bitmap = [1, 2, 3, 4]
                    }
                ]
            };

            var expectedPayload = new Ci.Agent.Payloads.CICodeCoveragePayload(settings);
            expectedPayload.TryProcessEvent(coveragePayload);
            var expectedFormItems = expectedPayload.ToArray();

            MultipartFormItem[] finalFormItems = null;
            sender.Setup(x => x.SendPayloadAsync(It.IsAny<Ci.Agent.Payloads.CICodeCoveragePayload>()))
                  .Returns<Ci.Agent.Payloads.CICodeCoveragePayload>(payload =>
                   {
                       finalFormItems = payload.ToArray();
                       return Task.CompletedTask;
                   });

            agentlessWriter.WriteEvent(coveragePayload);
            await agentlessWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            Assert.NotNull(finalFormItems);
            Assert.Equal(expectedFormItems.Length, finalFormItems.Length);
            for (var i = 0; i < expectedFormItems.Length; i++)
            {
                var finalItem = finalFormItems[i];
                var expectedItem = expectedFormItems[i];

                Assert.Equal(expectedItem.Name, finalItem.Name);
                Assert.Equal(expectedItem.ContentType, finalItem.ContentType);
                Assert.Equal(expectedItem.FileName, finalItem.FileName);
                Assert.True(finalItem.ContentInBytes.Value.ToArray().SequenceEqual(expectedItem.ContentInBytes.Value.ToArray()));
            }
        }

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public async Task AgentlessCodeCoverageCompressedPayloadTest()
        {
            var requestFactory = new HttpClientRequestFactory(new Uri("http://localhost"), Array.Empty<KeyValuePair<string, string>>(), new TestMessageHandler());
            var apiRequest = requestFactory.Create(new Uri("http://localhost/api"));
            var response = await apiRequest.PostAsync(
                                          new[] { new MultipartFormItem("TestName", "application/binary", "TestFileName", "TestContent"u8.ToArray()) },
                                          MultipartCompression.GZip)
                                     .ConfigureAwait(false);
            var stream = await response.GetStreamAsync().ConfigureAwait(false);
            using var unzippedStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            var ms = new MemoryStream();
            await unzippedStream.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;
            using var rs = new StreamReader(ms, Encoding.UTF8);
            var requestContent = await rs.ReadToEndAsync().ConfigureAwait(false);
            requestContent.Should().Contain("Content-Type: application/binary");
            requestContent.Should().Contain("Content-Disposition: form-data; name=TestName; filename=TestFileName; filename*=utf-8''TestFileName");
            requestContent.Should().Contain("TestContent");
        }
#endif

        [Fact]
        public async Task SlowSenderTest()
        {
            var settings = CIVisibility.Settings;
            var flushTcs = new TaskCompletionSource<bool>();

            var sender = new Mock<ICIVisibilityProtocolWriterSender>();
            var agentlessWriter = new CIVisibilityProtocolWriter(settings, sender.Object, concurrency: 1);
            var lstPayloads = new List<byte[]>();

            sender.Setup(x => x.SendPayloadAsync(It.IsAny<Ci.Agent.Payloads.CIVisibilityProtocolPayload>()))
                .Returns<Ci.Agent.Payloads.CIVisibilityProtocolPayload>(payload =>
                {
                    lstPayloads.Add(payload.ToArray());
                    return flushTcs.Task;
                });

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            var expectedPayload = new Ci.Agent.Payloads.CITestCyclePayload(settings);
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
        public async Task ConcurrencyFlushTest()
        {
            var settings = CIVisibility.Settings;
            var sender = new Mock<ICIVisibilityProtocolWriterSender>();
            // We set 8 threads of concurrency and a batch interval of 10 seconds to avoid the autoflush.
            var agentlessWriter = new CIVisibilityProtocolWriter(settings, sender.Object, concurrency: 8, batchInterval: 10_000);
            var lstPayloads = new List<byte[]>();

            const int numSpans = 2_000;

            sender.Setup(x => x.SendPayloadAsync(It.IsAny<Ci.Agent.Payloads.CIVisibilityProtocolPayload>()))
                  .Returns<Ci.Agent.Payloads.CIVisibilityProtocolPayload>(async payload =>
                   {
                       lock (lstPayloads)
                       {
                           lstPayloads.Add(payload.ToArray());
                       }

                       await Task.Delay(150).ConfigureAwait(false);
                   });

            for (ulong i = 0; i < numSpans; i++)
            {
                var span = new Span(new SpanContext(i, i), DateTimeOffset.UtcNow);
                agentlessWriter.WriteEvent(new SpanEvent(span));
            }

            lock (lstPayloads)
            {
                // We assert that the total spans has not been flushed yet
                Assert.NotEqual(numSpans, GetNumberOfSpans(lstPayloads));
            }

            // We force flush
            await agentlessWriter.FlushTracesAsync().ConfigureAwait(false);

            lock (lstPayloads)
            {
                // We assert that the total spans has been flushed after the FlushTraces class
                Assert.Equal(numSpans, GetNumberOfSpans(lstPayloads));
            }

            static int GetNumberOfSpans(List<byte[]> payloads)
            {
                int spans = 0;
                foreach (var payload in payloads)
                {
                    var payloadInJson = MessagePackSerializer.ToJson(payload);
                    var payloadObject = JsonConvert.DeserializeObject(payloadInJson);
                    spans += ((JObject)payloadObject)["events"].Count();
                }

                return spans;
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

        [Fact]
        public void CoverageBufferTest()
        {
            int headerSize = Ci.Agent.Payloads.EventsBuffer<Ci.IEvent>.HeaderSize + Ci.Agent.Payloads.MultipartPayload.HeaderSize;

            var settings = CIVisibility.Settings;

            int bufferSize = headerSize + 1024;
            int maxBufferSize = (int)(4.5 * 1024 * 1024);
            var coveragePayload = new TestCoverage
            {
                SessionId = 42,
                SuiteId = 56,
                SpanId = 84,
                Files =
                [
                    new FileCoverage
                    {
                        FileName = "MyFile",
                        Bitmap = [1, 2, 3, 4]
                    }
                ]
            };

            var coveragePayloadInBytes = MessagePackSerializer.Serialize<Ci.IEvent>(coveragePayload, Ci.Agent.MessagePack.CIFormatterResolver.Instance);

            while (bufferSize < maxBufferSize)
            {
                var payloadBuffer = new Ci.Agent.Payloads.CICodeCoveragePayload(settings, maxItemsPerPayload: int.MaxValue, maxBytesPerPayload: bufferSize);
                while (payloadBuffer.TryProcessEvent(coveragePayload))
                {
                    // .
                }

                // The number of items in the events should be the same as the num calculated
                // without decimals (items that doesn't fit doesn't get added)
                var numItemsTrunc = (bufferSize - headerSize) / coveragePayloadInBytes.Length;
                Assert.Equal(numItemsTrunc, payloadBuffer.Events.Count);

                bufferSize *= 2;
            }
        }

        internal class TestMessageHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var ms = new MemoryStream();
                await request.Content.CopyToAsync(ms).ConfigureAwait(false);
                var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
                responseMessage.Content = new ByteArrayContent(ms.ToArray());
                return responseMessage;
            }
        }
    }
}
