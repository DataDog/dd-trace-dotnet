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
        private AgentSpanWriter _agentSpanWriter;
        private Mock<IApi> _api;
        private Mock<IDatadogTracer> _tracer;

        public AgentWriterTests()
        {
            _tracer = new Mock<IDatadogTracer>();
            _tracer.Setup(x => x.DefaultServiceName).Returns("Default");
            var context = new Mock<ITraceContext>();
            _api = new Mock<IApi>();
            _agentSpanWriter = new AgentSpanWriter(_api.Object);
        }

        [Fact]
        public async Task WriteTrace_2Traces_SendToApi()
        {
            // TODO:bertrand it is too complicated to setup such a simple test
            var trace = new List<Span> { new Span(_tracer.Object, null, "Operation", "Service", null) };
            _agentSpanWriter.WriteTrace(trace);
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            _api.Verify(x => x.SendTracesAsync(It.Is<List<List<Span>>>(y => y.Single().Equals(trace))), Times.Once);

            trace = new List<Span> { new Span(_tracer.Object, null, "Operation2", "AnotherService", null) };
            _agentSpanWriter.WriteTrace(trace);
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            _api.Verify(x => x.SendTracesAsync(It.Is<List<List<Span>>>(y => y.Single().Equals(trace))), Times.Once);
        }
    }
}
