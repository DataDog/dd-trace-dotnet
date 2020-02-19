using System;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Datadog.Trace.Configuration;
using Datadog.Trace.Containers;
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
            string expectedContainedId = ContainerInfo.GetContainerId();
            string actualContainerId = null;
            var agentPortClaim = PortHelper.GetTcpPortClaim();

            using (var agent = new MockTracerAgent(agentPortClaim))
            {
                agent.RequestReceived += (sender, args) =>
                {
                    actualContainerId = args.Value.Request.Headers[AgentHttpHeaderNames.ContainerId];
                };

                var settings = new TracerSettings { AgentUri = agent.Uri };
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
