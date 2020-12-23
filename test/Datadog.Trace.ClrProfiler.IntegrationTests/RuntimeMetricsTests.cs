using System.Linq;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class RuntimeMetricsTests : TestHelper
    {
        public RuntimeMetricsTests(ITestOutputHelper output)
            : base("RuntimeMetrics", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsMetrics()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int statsdPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {statsdPort} for the statsdPort.");

            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "1");

            using (var agent = new MockTracerAgent(agentPort, statsdPort: statsdPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, statsdPort))
            {
                var requests = agent.StatsdRequests;

                // Exception metrics are pushed immediately
                var exceptionRequestsCount = requests.Count(r => r.Contains("runtime.dotnet.exceptions.count"));

                Assert.True(exceptionRequestsCount > 0, "No exception metrics received. Metrics received: " + string.Join("\n", requests));

                // Contention metrics are pushed every 10 seconds
                var contentionRequestsCount = requests.Count(r => r.Contains("runtime.dotnet.threads.contention_count"));

                Assert.True(contentionRequestsCount > 0, "No contention metrics received. Metrics received: " + string.Join("\n", requests));

                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");
            }
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void MetricsDisabled()
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            int statsdPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {statsdPort} for the statsdPort.");

            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "0");

            using (var agent = new MockTracerAgent(agentPort, statsdPort: statsdPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, statsdPort))
            {
                var requests = agent.StatsdRequests;

                Assert.True(requests.Count == 0, "Received metrics despite being disabled. Metrics received: " + string.Join("\n", requests));
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");
            }
        }
    }
}
