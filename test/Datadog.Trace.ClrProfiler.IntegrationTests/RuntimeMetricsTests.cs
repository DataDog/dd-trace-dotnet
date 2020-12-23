using System;
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

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");

            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "1");

            using var agent = new MockTracerAgent(agentPort, useStatsd: true);
            Output.WriteLine($"Assigning port {agent.StatsdPort} for the statsdPort.");

            using var processResult = RunSampleAndWaitForExit(agent.Port, agent.StatsdPort);
            var requests = agent.StatsdRequests;

            // Check if we receive 2 kinds of metrics:
            // - exception count is gathered using common .NET APIs
            // - contention count is gathered using platform-specific APIs

            var exceptionRequestsCount = requests.Count(r => r.Contains("runtime.dotnet.exceptions.count"));

            Assert.True(exceptionRequestsCount > 0, "No exception metrics received. Metrics received: " + string.Join("\n", requests));

            // Check if .NET Framework or .NET Core 3.1+
            if (!EnvironmentHelper.IsCoreClr()
             || (Environment.Version.Major == 3 && Environment.Version.Minor == 1)
             || Environment.Version.Major >= 5)
            {
                var contentionRequestsCount = requests.Count(r => r.Contains("runtime.dotnet.threads.contention_count"));

                Assert.True(contentionRequestsCount > 0, "No contention metrics received. Metrics received: " + string.Join("\n", requests));
            }

            Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void MetricsDisabled()
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");

            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "0");

            using var agent = new MockTracerAgent(agentPort, useStatsd: true);
            Output.WriteLine($"Assigning port {agent.StatsdPort} for the statsdPort.");

            using var processResult = RunSampleAndWaitForExit(agent.Port, agent.StatsdPort);
            var requests = agent.StatsdRequests;

            Assert.True(requests.Count == 0, "Received metrics despite being disabled. Metrics received: " + string.Join("\n", requests));
            Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");
        }
    }
}
