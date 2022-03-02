// <copyright file="CIAgentWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Agent.MessagePack;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI.Agent
{
    public class CIAgentWriterTests
    {
        private readonly CIAgentWriter _ciAgentWriter;
        private readonly Configuration.ImmutableTracerSettings _settings;
        private readonly Mock<IApi> _api;

        public CIAgentWriterTests()
        {
            var tracer = new Mock<IDatadogTracer>();
            tracer.Setup(x => x.DefaultServiceName).Returns("Default");
            _settings = new Configuration.ImmutableTracerSettings(Ci.Configuration.CIVisibilitySettings.FromDefaultSources().TracerSettings);

            _api = new Mock<IApi>();
            _ciAgentWriter = new CIAgentWriter(_api.Object);
        }

        [Fact]
        public async Task WriteTrace_2Traces_SendToApi()
        {
            var trace = new[] { new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow) };
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(trace, SpanFormatterResolver.Instance);

            _ciAgentWriter.WriteTrace(new ArraySegment<Span>(trace));
            await _ciAgentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData1)), It.Is<int>(i => i == 1)), Times.Once);

            _api.Invocations.Clear();

            trace = new[] { new Span(new SpanContext(2, 2), DateTimeOffset.UtcNow) };
            var expectedData2 = Vendors.MessagePack.MessagePackSerializer.Serialize(trace, SpanFormatterResolver.Instance);

            _ciAgentWriter.WriteTrace(new ArraySegment<Span>(trace));
            await _ciAgentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData2)), It.Is<int>(i => i == 1)), Times.Once);

            await _ciAgentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushTwice()
        {
            var w = new CIAgentWriter(_api.Object);
            await w.FlushAndCloseAsync();
            await w.FlushAndCloseAsync();
        }

        private static bool Equals(ArraySegment<byte> data, byte[] expectedData)
        {
            return data.Array.Skip(data.Offset).Take(data.Count).Skip(SpanBuffer.HeaderSize).SequenceEqual(expectedData);
        }
    }
}
