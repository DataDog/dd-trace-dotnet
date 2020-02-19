using System.IO;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AzureAppServices
{
    public class FakeAzureAppServicesTests : TestHelper
    {
        private const string SampleName = "FakeAzureAppServices";

        public FakeAzureAppServicesTests(ITestOutputHelper output)
            : base(SampleName, output)
        {
            SetEnvironmentVariable("DD_AZURE_APP_SERVICES", "1");
        }

        [Fact(Skip = "TODO: Traces from the sub process are not coming through")]
        [Trait("Category", "EndToEnd")]
        public void DoesStartNestedProcess()
        {
            var testProcess = EnvironmentHelper.GetSampleApplicationPath();
            var fakeTraceAgentPath = testProcess.Replace($"{SampleName}.exe", "FakeTraceAgent.exe");

            SetEnvironmentVariable("DD_TRACE_AGENT_PATH", fakeTraceAgentPath);

            var agentPortClaim = PortHelper.GetTcpPortClaim();
            using (var agent = new MockTracerAgent(agentPortClaim))
            using (var processResult = RunSampleAndWaitForExit(agent.Port))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");
                var spans = agent.WaitForSpans(4);
                Assert.Equal(expected: 4, spans.Count);
            }
        }
    }
}
