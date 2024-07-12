// <copyright file="ApmAgentWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Ci.Agent;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI.Agent
{
    public class ApmAgentWriterTests
    {
        private readonly ApmAgentWriter _ciAgentWriter;
        private readonly Configuration.ImmutableTracerSettings _settings;
        private readonly Mock<IApi> _api;

        public ApmAgentWriterTests()
        {
            var tracer = new Mock<IDatadogTracer>();
            tracer.Setup(x => x.DefaultServiceName).Returns("Default");
            _settings = new Configuration.ImmutableTracerSettings(Ci.Configuration.CIVisibilitySettings.FromDefaultSources().TracerSettings);

            _api = new Mock<IApi>();
            _ciAgentWriter = new ApmAgentWriter(_api.Object);
        }

        [Fact]
        public async Task WriteTrace_2Traces_SendToApi()
        {
            var spans1 = new ArraySegment<Span>(new[] { new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow) });
            var traceChunk1 = new TraceChunkModel(spans1);
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(traceChunk1, SpanFormatterResolver.Instance);

            _ciAgentWriter.WriteTrace(spans1);
            await _ciAgentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData1)), It.Is<int>(i => i == 1), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Once);
            _api.Invocations.Clear();

            var spans2 = new ArraySegment<Span>(new[] { new Span(new SpanContext(2, 2), DateTimeOffset.UtcNow) });
            var traceChunk2 = new TraceChunkModel(spans2);
            var expectedData2 = Vendors.MessagePack.MessagePackSerializer.Serialize(traceChunk2, SpanFormatterResolver.Instance);

            _ciAgentWriter.WriteTrace(spans2);
            await _ciAgentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData2)), It.Is<int>(i => i == 1), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Once);

            await _ciAgentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushTwice()
        {
            var w = new ApmAgentWriter(_api.Object);
            await w.FlushAndCloseAsync();
            await w.FlushAndCloseAsync();
        }

        private static bool Equals(ArraySegment<byte> data, byte[] expectedData)
        {
            return data.Array!.Skip(data.Offset).Take(data.Count).Skip(SpanBuffer.HeaderSize).SequenceEqual(expectedData);
        }
    }
}
