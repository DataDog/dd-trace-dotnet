using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests
{
    public class DatadogHttpClientTests
    {
        private readonly ITestOutputHelper _output;

        public DatadogHttpClientTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task DatadogHttpClient_CanSendTracesToAgent()
        {
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                agent.RequestDeserialized += (sender, args) =>
                {
                    var traces = args.Value.Select(
                        trace => string.Join(", ", trace.Select(span => $"{span.Name}.{span.Resource}.{span.SpanId}")));
                    _output.WriteLine($"Received {args.Value.Count} traces: {string.Join(";", traces)}");
                };

                var settings = new TracerSettings
                {
                    AgentUri = new Uri($"http://localhost:{agent.Port}"),
                    TracesTransport = TransportStrategy.DatadogTcp,
                };
                var tracer = new Tracer(settings);

                using (var scope = tracer.StartActive("operationName"))
                {
                    scope.Span.ResourceName = "resourceName";
                }

                // When this is added in, the test deadlocks! :scream:
                await tracer.FlushAsync();

                var spans = agent.WaitForSpans(1);
                Assert.Equal(1, spans.Count);
            }
        }
    }
}
