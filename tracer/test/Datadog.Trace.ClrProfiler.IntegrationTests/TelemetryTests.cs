// <copyright file="TelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class TelemetryTests : TestHelper
    {
        private const int ExpectedSpans = 3;
        private const string ServiceVersion = "1.0.0";

        public TelemetryTests(ITestOutputHelper output)
            : base("Telemetry", output)
        {
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.ActivityListenerEnabled, "true");
            SetServiceVersion(ServiceVersion);
            EnableDebugMode();
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task Telemetry_Agentless_IsSentOnAppClose()
        {
            using var agent = MockTracerAgent.Create(useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            using var telemetry = new MockTelemetryAgent<TelemetryData>();
            Output.WriteLine($"Assigned port {telemetry.Port} for the telemetry port.");
            EnableTelemetry(standaloneAgentPort: telemetry.Port);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(ExpectedSpans);

                await AssertExpectedSpans(spans);
            }

            var data = telemetry.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            AssertTelemetry(data);
            agent.Telemetry.Should().BeEmpty();
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task Telemetry_WithAgentProxy_IsSentOnAppClose()
        {
            using var agent = MockTracerAgent.Create(useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            EnableTelemetry();

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            var data = agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            AssertTelemetry(data);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task WhenDisabled_DoesntSendTelemetry()
        {
            using var agent = MockTracerAgent.Create(useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            EnableTelemetry(enabled: false);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            // Shouldn't have any, but wait for 5s
            agent.WaitForLatestTelemetry(x => true);
            agent.Telemetry.Should().BeEmpty();
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task WhenUsingNamedPipesAgent_UsesNamedPipesTelemetry()
        {
            if (!EnvironmentTools.IsWindows())
            {
                throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
            }

            EnvironmentHelper.EnableWindowsNamedPipes();
            // The server implementation of named pipes is flaky so have 3 attempts
            var attemptsRemaining = 3;
            while (true)
            {
                try
                {
                    attemptsRemaining--;
                    await RunTest();
                    return;
                }
                catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
                {
                    Output.WriteLine($"Error executing test. {attemptsRemaining} attempts remaining. {ex}");
                }
            }

            async Task RunTest()
            {
                using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
                agent.Output = Output;

                int httpPort = TcpPortProvider.GetOpenPort();
                Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
                using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
                {
                    Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                    var spans = agent.WaitForSpans(ExpectedSpans);
                    await AssertExpectedSpans(spans);
                }

                var data = agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);

                AssertTelemetry(data);
            }
        }

#if NETCOREAPP3_1_OR_GREATER
        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task WhenUsingUdsAgent_UsesUdsTelemetry()
        {
            EnvironmentHelper.EnableUnixDomainSockets();
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            var data = agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);

            AssertTelemetry(data);
        }
#endif

        private static void AssertTelemetry(TelemetryData data)
        {
            data.Application.ServiceVersion.Should().Be(ServiceVersion);
            data.Application.ServiceName.Should().Be("Samples.Telemetry");
        }

        private static async Task AssertExpectedSpans(IImmutableList<MockSpan> spans)
        {
            await VerifyHelper.VerifySpans(spans, VerifyHelper.GetSpanVerifierSettings())
                              .DisableRequireUniquePrefix()
                              .UseFileName("TelemetryTests");
        }
    }
}
