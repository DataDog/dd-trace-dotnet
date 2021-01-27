using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class AgentWriterTests
    {
        private readonly AgentWriter _agentWriter;
        private readonly Mock<IApi> _api;
        private readonly SpanContext _spanContext;

        public AgentWriterTests()
        {
            var tracer = new Mock<IDatadogTracer>();
            tracer.Setup(x => x.DefaultServiceName).Returns("Default");

            _api = new Mock<IApi>();
            _agentWriter = new AgentWriter(_api.Object, statsd: null);

            var parentSpanContext = new Mock<ISpanContext>();
            var traceContext = new Mock<ITraceContext>();
            _spanContext = new SpanContext(parentSpanContext.Object, traceContext.Object, serviceName: null);
        }

        [Fact]
        public async Task WriteTrace_2Traces_SendToApi()
        {
            var trace = new[] { new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow) };
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(trace, new FormatterResolverWrapper(SpanFormatterResolver.Instance));

            _agentWriter.WriteTrace(trace);
            await _agentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData1)), It.Is<int>(i => i == 1)), Times.Once);

            _api.ResetCalls();

            trace = new[] { new Span(new SpanContext(2, 2), DateTimeOffset.UtcNow) };
            var expectedData2 = Vendors.MessagePack.MessagePackSerializer.Serialize(trace, new FormatterResolverWrapper(SpanFormatterResolver.Instance));

            _agentWriter.WriteTrace(trace);
            await _agentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData2)), It.Is<int>(i => i == 1)), Times.Once);
        }

        [Fact]
        public async Task FlushTwice()
        {
            var w = new AgentWriter(_api.Object, statsd: null);
            await w.FlushAndCloseAsync();
            await w.FlushAndCloseAsync();
        }

        private bool Equals(ArraySegment<byte> data, byte[] expectedData)
        {
            return data.Array.Skip(data.Offset).Take(data.Count).Skip(SpanBuffer.HeaderSize).SequenceEqual(expectedData);
        }
    }
}
