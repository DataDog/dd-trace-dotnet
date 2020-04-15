using System.Threading;
using Datadog.Core.Tools;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AzureAppServices
{
    public class FakeKuduTests : TestHelper
    {
        public FakeKuduTests(ITestOutputHelper output)
            : base("FakeKudu", output)
        {
            SetEnvironmentVariable(PlatformHelpers.AzureAppServices.AzureAppServicesContextKey, "1");
            SetEnvironmentVariable("APP_POOL_ID", "~1KuduScmProcessIsFilteredByTilde");
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void DoesNotSubmitTraces()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");
                Thread.Sleep(1000); // Give any traces a bit to come through
                Assert.Empty(agent.Spans);
            }
        }
    }
}
