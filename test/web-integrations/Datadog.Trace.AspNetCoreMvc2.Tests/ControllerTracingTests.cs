using System;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.AspNetCoreMvc2.Tests
{
    public class ControllerTracingTests
        : IClassFixture<CustomWebApplicationFactory<Samples.AspNetCoreMvc2.Startup>>
    {
        private readonly CustomWebApplicationFactory<Samples.AspNetCoreMvc2.Startup> _factory;

        public ControllerTracingTests(CustomWebApplicationFactory<Samples.AspNetCoreMvc2.Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ExceptionIsCaught()
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            var environmentMetadata = new EnvironmentMetadata(this);
            var integrationsPath = environmentMetadata.GetIntegrationsFilePaths();
            var profilerPath = environmentMetadata.GetProfilerPath();

            using (var agent = new MockTracerAgent(agentPort))
            {
                var client = _factory.CreateClient();
                Exception caught = null;
                try
                {
                    var response = await client.GetAsync("/bad-request");
                }
                catch (Exception ex)
                {
                    caught = ex;
                }

                Assert.NotNull(caught);

                var activeScope = Tracer.Instance.ActiveScope;

                Assert.NotNull(activeScope);
            }
        }
    }
}
