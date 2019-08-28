using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests
{
    public class ContainerTaggingTests
    {
        private readonly ITestOutputHelper _output;

        public ContainerTaggingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Foo()
        {
            if (EnvironmentHelper.IsWindows())
            {
                _output.WriteLine("Ignored for Windows OS for now.");
                return;
            }

            string containerId = null;
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                agent.RequestReceived += (sender, args) =>
                {
                    containerId = args.Value.Request.Headers[AgentHttpHeaderNames.ContainerId];
                };

                var settings = new TracerSettings { AgentUri = new Uri($"http://localhost:{agentPort}") };
                var tracer = new Tracer(settings);

                using (var scope = tracer.StartActive("operationName"))
                {
                    scope.Span.ResourceName = "resourceName";
                }

                await tracer.FlushAsync();

                var spans = agent.WaitForSpans(1);
                Assert.Equal(1, spans.Count);
                Assert.NotNull(containerId);
                Assert.NotEqual(string.Empty, containerId);
            }
        }
    }
}
