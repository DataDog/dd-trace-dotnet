using System;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
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
        public async Task Http_Headers_Contain_ContainerId()
        {
            string expectedContainedId = ContainerMetadata.GetContainerId();
            string actualContainerId = null;
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                agent.RequestReceived += (sender, args) =>
                {
                    actualContainerId = args.Value.Request.Headers[AgentHttpHeaderNames.ContainerId];
                };

                var settings = new TracerSettings { AgentUri = new Uri($"http://localhost:{agent.Port}") };
                var tracer = new Tracer(settings);

                using (var scope = tracer.StartActive("operationName"))
                {
                    scope.Span.ResourceName = "resourceName";
                }

                await tracer.FlushAsync();

                var spans = agent.WaitForSpans(1);
                Assert.Equal(1, spans.Count);
                Assert.Equal(expectedContainedId, actualContainerId);

                if (EnvironmentTools.IsWindows())
                {
                    // we don't extract the containerId on Windows (yet?)
                    Assert.Null(actualContainerId);
                }
            }
        }
    }
}
