// <copyright file="TelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class TelemetryTests : TestHelper
    {
        public TelemetryTests(ITestOutputHelper output)
            : base("Telemetry", output)
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void Telemetry_IsSentOnAppClose()
        {
            const string expectedOperationName = "http.request";
            const int expectedSpanCount = 1;
            const string serviceVersion = "1.0.0";

            int agentPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            using var agent = new MockTracerAgent(agentPort);

            SetServiceVersion(serviceVersion);
            using var telemetry = this.ConfigureTelemetry();

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);
            }

            var data = telemetry.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);

            data.Application.ServiceVersion.Should().Be(serviceVersion);
            data.Application.ServiceName.Should().Be("Samples.Telemetry");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void WhenDisabled_DoesntSendTelemetry()
        {
            const string expectedOperationName = "http.request";
            const int expectedSpanCount = 1;
            const string serviceVersion = "1.0.0";

            int agentPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            using var agent = new MockTracerAgent(agentPort);

            SetServiceVersion(serviceVersion);
            int telemetryPort = TcpPortProvider.GetOpenPort();
            using var telemetry = new MockTelemetryAgent<TelemetryData>(telemetryPort);

            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "false");
            SetEnvironmentVariable("DD_TRACE_TELEMETRY_URL", $"http://localhost:{telemetry.Port}");
            SetEnvironmentVariable("DD_API_KEY", "INVALID_KEY_FOR_TESTS");

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);
            }

            // Shouldn't have any, but wait for 5s
            telemetry.WaitForLatestTelemetry(x => true);
            telemetry.Telemetry.Should().BeEmpty();
        }

#if NETCOREAPP3_1_OR_GREATER
        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void WhenUsingUdsAgent_DoesntSendTelemetry()
        {
            const string expectedOperationName = "http.request";
            const int expectedSpanCount = 1;
            const string serviceVersion = "1.0.0";

            EnvironmentHelper.TransportType = TestTransports.Uds;
            using var agent = EnvironmentHelper.GetMockAgent();

            SetServiceVersion(serviceVersion);

            int telemetryPort = TcpPortProvider.GetOpenPort();
            using var telemetry = new MockTelemetryAgent<TelemetryData>(telemetryPort);

            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "false");

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);
            }

            // Shouldn't have any, but wait for 5s
            telemetry.WaitForLatestTelemetry(x => true);
            telemetry.Telemetry.Should().BeEmpty();
        }
#endif
    }
}
