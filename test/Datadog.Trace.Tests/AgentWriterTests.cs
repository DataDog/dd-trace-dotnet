using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class AgentWriterTests
    {
        [Fact]
        public async Task WriteTrace_2Traces_SendToApi()
        {
            var api = new Mock<IApi>();
            var agentWriter = new AgentWriter(api.Object, statsd: null);

            var spanContext = new SpanContext(Mock.Of<ISpanContext>(), Mock.Of<ITraceContext>(), serviceName: null);

            // TODO:bertrand it is too complicated to setup such a simple test
            var trace = new[] { new Span(spanContext, start: null) };
            agentWriter.WriteTrace(trace);
            await Task.Delay(TimeSpan.FromSeconds(2));
            api.Verify(x => x.SendTracesAsync(It.Is<Span[][]>(y => y.Single().Equals(trace))), Times.Once);

            trace = new[] { new Span(spanContext, start: null) };
            agentWriter.WriteTrace(trace);
            await Task.Delay(TimeSpan.FromSeconds(2));
            api.Verify(x => x.SendTracesAsync(It.Is<Span[][]>(y => y.Single().Equals(trace))), Times.Once);
        }

        [Fact]
        public async Task FlushTwice()
        {
            var w = new AgentWriter(Mock.Of<IApi>(), statsd: null);
            await w.FlushAndCloseAsync();
            await w.FlushAndCloseAsync();
        }
    }
}
