// <copyright file="TelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
        private const bool DependenciesEnabledDefault = true;
        private readonly ITestOutputHelper _output;

        public TelemetryTests(ITestOutputHelper output)
            : base("Telemetry", output)
        {
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, "true");
            SetServiceVersion(ServiceVersion);
            _output = output;
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Telemetry_Agentless_IsSentOnAppClose(bool? enableDependencies)
        {
            using var agent = MockTracerAgent.Create(Output, useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            using var telemetry = new MockTelemetryAgent();
            Output.WriteLine($"Assigned port {telemetry.Port} for the telemetry port.");
            EnableAgentlessTelemetry(telemetry.Port, enableDependencies);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);

                await AssertExpectedSpans(spans);
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            telemetry.AssertConfiguration(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
            AssertService(telemetry, "Samples.Telemetry", ServiceVersion);
            AssertDependencies(telemetry, enableDependencies);
            agent.Telemetry.Should().BeEmpty();
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Telemetry_WithAgentProxy_IsSentOnAppClose(bool? enableDependencies)
        {
            using var agent = MockTracerAgent.Create(Output, useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            EnableAgentProxyTelemetry(enableDependencies);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            agent.AssertConfiguration(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
            AssertService(agent, "Samples.Telemetry", ServiceVersion);
            AssertDependencies(agent, enableDependencies);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task WhenDisabled_DoesntSendTelemetry()
        {
            using var agent = MockTracerAgent.Create(Output, useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            // disabled by default in integration tests, but make sure
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "false");

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            // Shouldn't have any, but wait for 5s
            agent.WaitForLatestTelemetry(x => true);
            agent.Telemetry.Should().BeEmpty();
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WhenUsingNamedPipesAgent_UsesNamedPipesTelemetry(bool? enableDependencies)
        {
            if (!EnvironmentTools.IsWindows())
            {
                throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
            }

            EnvironmentHelper.EnableWindowsNamedPipes();
            EnableDependencies(enableDependencies);

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
                    await ReportRetry(_output, attemptsRemaining, this.GetType(), ex);
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
                    ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                    var spans = agent.WaitForSpans(ExpectedSpans);
                    await AssertExpectedSpans(spans);
                }

                agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
                agent.AssertConfiguration(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
                AssertService(agent, "Samples.Telemetry", ServiceVersion);
                AssertDependencies(agent, enableDependencies);
            }
        }

#if NETCOREAPP3_1_OR_GREATER
        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WhenUsingUdsAgent_UsesUdsTelemetry(bool? enableDependencies)
        {
            EnvironmentHelper.EnableUnixDomainSockets();
            EnableDependencies(enableDependencies);
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            AssertService(agent, "Samples.Telemetry", ServiceVersion);
            AssertDependencies(agent, enableDependencies);
        }
#endif

        private static void AssertService(MockTracerAgent mockAgent, string expectedServiceName, string expectedServiceVersion)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryData)x).RequestType == TelemetryRequestTypes.AppStarted);
            AssertService(mockAgent.Telemetry.Cast<TelemetryData>(), expectedServiceName, expectedServiceVersion);
        }

        private static void AssertService(MockTelemetryAgent telemetry, string expectedServiceName, string expectedServiceVersion)
        {
            telemetry.WaitForLatestTelemetry(x => x.RequestType == TelemetryRequestTypes.AppStarted);
            AssertService(telemetry.Telemetry, expectedServiceName, expectedServiceVersion);
        }

        private static void AssertService(IEnumerable<TelemetryData> allData, string expectedServiceName, string expectedServiceVersion)
        {
            var appClosing = allData.Should().ContainSingle(x => x.RequestType == TelemetryRequestTypes.AppClosing).Subject;
            appClosing.Application.ServiceName.Should().Be(expectedServiceName);
            appClosing.Application.ServiceVersion.Should().Be(expectedServiceVersion);
        }

        private static void AssertDependencies(MockTracerAgent mockAgent, bool? enableDependencies)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryData)x).RequestType == TelemetryRequestTypes.AppClosing);
            AssertDependencies(mockAgent.Telemetry.Cast<TelemetryData>(), enableDependencies);
        }

        private static void AssertDependencies(MockTelemetryAgent telemetry, bool? enableDependencies)
        {
            telemetry.WaitForLatestTelemetry(x => x.RequestType == TelemetryRequestTypes.AppClosing);
            AssertDependencies(telemetry.Telemetry, enableDependencies);
        }

        private static void AssertDependencies(IEnumerable<TelemetryData> allData, bool? enableDependencies)
        {
            var enabled = (enableDependencies ?? DependenciesEnabledDefault);

            var dependencies = allData
                         .Where(x => x.Payload is AppStartedPayload { Dependencies: not null }
                                  || x.Payload is AppDependenciesLoadedPayload);

            if (enableDependencies ?? DependenciesEnabledDefault)
            {
                dependencies.Should().NotBeEmpty();
            }
            else
            {
                dependencies.Should().BeEmpty();
            }
        }

        private static async Task AssertExpectedSpans(IImmutableList<MockSpan> spans)
        {
            await VerifyHelper.VerifySpans(spans, VerifyHelper.GetSpanVerifierSettings())
                              .DisableRequireUniquePrefix()
                              .UseFileName("TelemetryTests");
        }

        private void EnableAgentlessTelemetry(int standaloneAgentPort, bool? enableDependencies)
        {
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "true");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED", "true");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENT_PROXY_ENABLED", "false");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_URL", $"http://localhost:{standaloneAgentPort}");
            // API key is required for agentless
            SetEnvironmentVariable("DD_API_KEY", "INVALID_KEY_FOR_TESTS");
            EnableDependencies(enableDependencies);
        }

        private void EnableAgentProxyTelemetry(bool? enableDependencies)
        {
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "true");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED", "false");
            EnableDependencies(enableDependencies);
        }

        private void EnableDependencies(bool? enableDependencies)
        {
            SetEnvironmentVariable(ConfigurationKeys.Telemetry.DependencyCollectionEnabled, (enableDependencies ?? DependenciesEnabledDefault).ToString());
        }
    }
}
