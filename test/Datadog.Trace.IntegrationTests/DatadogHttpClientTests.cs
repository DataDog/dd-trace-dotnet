using System;
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
        public void DatadogHttpClient_CanSendTracesToAgent()
        {
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                agent.RequestDeserialized += (sender, args) =>
                {
                    _output.WriteLine($"Received {args.Value.Count} traces");
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
                // await tracer.FlushAsync();

                var spans = agent.WaitForSpans(1);
                Assert.Equal(1, spans.Count);
            }
        }
    }
}
