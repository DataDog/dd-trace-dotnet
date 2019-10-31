using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
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
            _agentWriter = new AgentWriter(_api.Object, dogStatsdClient: null);

            var parentSpanContext = new Mock<ISpanContext>();
            var traceContext = new Mock<ITraceContext>();
            _spanContext = new SpanContext(parentSpanContext.Object, traceContext.Object, serviceName: null);
        }

        [Fact]
        public async Task WriteTrace_2Traces_SendToApi()
        {
            // TODO:bertrand it is too complicated to setup such a simple test
            var trace = new List<Span> { new Span(_spanContext, start: null) };
            _agentWriter.WriteTrace(trace);
            await Task.Delay(TimeSpan.FromSeconds(2));
            _api.Verify(x => x.SendTracesAsync(It.Is<List<List<Span>>>(y => y.Single().Equals(trace))), Times.Once);

            trace = new List<Span> { new Span(_spanContext, start: null) };
            _agentWriter.WriteTrace(trace);
            await Task.Delay(TimeSpan.FromSeconds(2));
            _api.Verify(x => x.SendTracesAsync(It.Is<List<List<Span>>>(y => y.Single().Equals(trace))), Times.Once);
        }

        [Fact]
        public async Task FlushTwice()
        {
            var w = new AgentWriter(_api.Object, dogStatsdClient: null);
            await w.FlushAndCloseAsync();
            await w.FlushAndCloseAsync();
        }
    }
}
