// <copyright file="TelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.Telemetry.Metrics;
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
        private const int ExpectedTraces = 2;
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

        public static TheoryData<bool?> Data => new() { true, false, null, };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task Telemetry_Agentless_IsSentOnAppClose(bool? enableDependencies)
        {
            using var agent = MockTracerAgent.Create(Output, useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            using var telemetry = new MockTelemetryAgent();
            Output.WriteLine($"Assigned port {telemetry.Port} for the telemetry port.");
            EnableAgentlessTelemetry(telemetry.Port, enableDependencies);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);

                await AssertExpectedSpans(spans);
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            telemetry.AssertConfiguration(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
            telemetry.AssertConfiguration(ConfigurationKeys.PropagationStyleExtract, "Datadog,tracecontext");
            telemetry.AssertConfiguration(ConfigurationKeys.PropagationStyleInject, "Datadog,tracecontext");

            AssertService(telemetry, "Samples.Telemetry", ServiceVersion);
            AssertDependencies(telemetry, enableDependencies);
            AssertNoRedactedErrorLogs(telemetry);
            agent.Telemetry.Should().BeEmpty();
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task Telemetry_WithAgentProxy_IsSentOnAppClose(bool? enableDependencies)
        {
            using var agent = MockTracerAgent.Create(Output, useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            EnableAgentProxyTelemetry(enableDependencies);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            agent.AssertConfiguration(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
            agent.AssertConfiguration(ConfigurationKeys.PropagationStyleExtract, "Datadog,tracecontext");
            agent.AssertConfiguration(ConfigurationKeys.PropagationStyleInject, "Datadog,tracecontext");

            AssertService(agent, "Samples.Telemetry", ServiceVersion);
            AssertDependencies(agent, enableDependencies);
            AssertNoRedactedErrorLogs(agent);
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
            using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            // Shouldn't have any, but wait for 5s
            agent.WaitForLatestTelemetry(x => true);
            agent.Telemetry.Should().BeEmpty();
            AssertNoRedactedErrorLogs(agent);
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task WhenUsingNamedPipesAgent_UsesNamedPipesTelemetry(bool? enableDependencies)
        {
            if (!EnvironmentTools.IsWindows())
            {
                throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
            }

            EnvironmentHelper.EnableWindowsNamedPipes();
            EnableAgentProxyTelemetry(enableDependencies);

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
                    await ReportRetry(_output, attemptsRemaining, ex);
                }
            }

            async Task RunTest()
            {
                using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
                agent.Output = Output;

                int httpPort = TcpPortProvider.GetOpenPort();
                Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
                using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
                {
                    ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                    var spans = agent.WaitForSpans(ExpectedSpans);
                    await AssertExpectedSpans(spans);
                }

                agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
                agent.AssertConfiguration(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
                AssertService(agent, "Samples.Telemetry", ServiceVersion);
                AssertDependencies(agent, enableDependencies);
                AssertNoRedactedErrorLogs(agent);
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
            if (EnvironmentTools.IsWindows())
            {
                throw new SkipException("Trace agent doesn't support UDS on Windows, so this test isn't needed, even though it works (but is slightly flaky)");
            }

            EnvironmentHelper.EnableUnixDomainSockets();
            EnableDependencies(enableDependencies);
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            AssertService(agent, "Samples.Telemetry", ServiceVersion);
            AssertDependencies(agent, enableDependencies);
            AssertNoRedactedErrorLogs(agent);
        }
#endif

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task Telemetry_SendsMetrics()
        {
            using var agent = MockTracerAgent.Create(Output, useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            using var telemetry = new MockTelemetryAgent();
            Output.WriteLine($"Assigned port {telemetry.Port} for the telemetry port.");
            EnableAgentlessTelemetry(telemetry.Port, enableDependencies: true);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            // The numbers here may change, but we should have _some_
            telemetry.GetDistributions(DistributionShared.InitTime.GetName()).Sum(x => x.Points.Count).Should().BeGreaterThan(4);

            telemetry.GetMetricDataPoints(Count.TraceChunkEnqueued.GetName()).Sum(x => x.Value).Should().Be(ExpectedTraces);

            // The exact number of logs aren't important, but we should have some
            telemetry.GetMetricDataPoints(Count.LogCreated.GetName(), "level:info")
                     .Sum(x => x.Value).Should().BeGreaterThan(10);

            // Should have at least 1 call to the Agentless telemetry API and no errors
            var telemetryRequests = telemetry.GetMetricDataPoints(Count.TelemetryApiRequests.GetName(), "endpoint:agentless")
                                             .Sum(x => x.Value);
            telemetryRequests.Should().BeGreaterThan(0);
            telemetry.GetMetricDataPoints(Count.TelemetryApiResponses.GetName(), "endpoint:agentless")
                     .Sum(x => x.Value).Should().Be(telemetryRequests);
            telemetry.GetMetricDataPoints(Count.TelemetryApiRequests.GetName(), "endpoint:agent").Should().BeEmpty();
            telemetry.GetMetricDataPoints(Count.TelemetryApiErrors.GetName()).Should().BeEmpty();

            // we inject and extract headers once
            // avoiding checking the specific tag + count here so this doesn't need updating if we change the defaults
            telemetry.GetMetricDataPoints(Count.ContextHeaderStyleInjected.GetName())
                     .Should()
                     .NotBeEmpty()
                     .And.OnlyContain(x => x.Tags.Length > 0)
                     .And.Subject.Select(x => string.Join(",", x.Tags))
                     .Distinct()
                     .Should()
                     .HaveCountGreaterOrEqualTo(1);

            // hopefully no errors
            telemetry.GetMetricDataPoints(CountShared.IntegrationsError.GetName()).Should().BeEmpty();
            telemetry.GetMetricDataPoints(Count.VersionConflictTracerCreated.GetName()).Should().BeEmpty();

            telemetry.GetMetricDataPoints(Gauge.Instrumentations.GetName()).Sum(x => x.Value).Should().BeGreaterThan(1);

            telemetry.GetMetricDataPoints(Count.SpanCreated.GetName()).Sum(x => x.Value).Should().Be(ExpectedSpans);
            telemetry.GetMetricDataPoints(Count.SpanFinished.GetName()).Sum(x => x.Value).Should().Be(ExpectedSpans);
            telemetry.GetMetricDataPoints(Count.SpanEnqueuedForSerialization.GetName()).Sum(x => x.Value).Should().Be(ExpectedSpans);
            telemetry.GetMetricDataPoints(Count.TraceSegmentCreated.GetName()).Sum(x => x.Value).Should().Be(ExpectedTraces);
            telemetry.GetMetricDataPoints(Count.TraceChunkEnqueued.GetName()).Sum(x => x.Value).Should().Be(ExpectedTraces);
            telemetry.GetMetricDataPoints(Count.TraceChunkSent.GetName()).Sum(x => x.Value).Should().Be(ExpectedTraces);

            telemetry.GetMetricDataPoints(Count.TraceApiRequests.GetName()).Sum(x => x.Value).Should().BeGreaterThan(0);
            telemetry.GetMetricDataPoints(Count.TraceApiResponses.GetName()).Sum(x => x.Value).Should().BeGreaterThan(0);
            telemetry.GetMetricDataPoints(Count.TraceApiErrors.GetName()).Should().BeEmpty();
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task Telemetry_SendsRedactedErrorLogs()
        {
            // we don't want to record the logs for this test, otherwise they'll cause the CheckLogsForErrors
            // stage to fail in CI. So instead, we write the logs to a temp directory which we won't check
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, Path.GetTempPath());

            using var agent = MockTracerAgent.Create(Output, useTelemetry: true);
            Output.WriteLine($"Assigned port {agent.Port} for the agentPort.");

            using var telemetry = new MockTelemetryAgent();
            Output.WriteLine($"Assigned port {telemetry.Port} for the telemetry port.");

            EnableAgentlessTelemetry(telemetry.Port, enableDependencies: true);
            SetEnvironmentVariable(ConfigurationKeys.Telemetry.TelemetryLogsEnabled, "1");
            // Create invalid sampling rules (invalid JSON) to trigger parsing error
            SetEnvironmentVariable(ConfigurationKeys.CustomSamplingRules, "[{\"sample_rate\":0.1");

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            WaitForAllTelemetry(telemetry);
            telemetry.Telemetry
                     .Where(x => x.IsRequestType(TelemetryRequestTypes.RedactedErrorLogs))
                     .Should()
                     .NotBeEmpty();

            var allLogs = telemetry.Telemetry
                                   .OrderBy(x => x.SeqId)
                                   .Select(x => x.TryGetPayload<LogsPayload>(TelemetryRequestTypes.RedactedErrorLogs))
                                   .Where(x => x is not null)
                                   .SelectMany(x => x.Logs)
                                   .ToList();

            // a debug log created in Instrumentation.cs
            allLogs.Should()
                   .ContainSingle()
                   .Which.Message.Should()
                   .Be("Unable to parse the trace sampling rules.");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task Telemetry_CollectsInstallSignature()
        {
            var expectedInstallId = "install id";
            var expectedInstallType = "install type";
            var expectedInstallTime = "install time";

            SetEnvironmentVariable("DD_INSTRUMENTATION_INSTALL_ID", expectedInstallId);
            SetEnvironmentVariable("DD_INSTRUMENTATION_INSTALL_TYPE", expectedInstallType);
            SetEnvironmentVariable("DD_INSTRUMENTATION_INSTALL_TIME", expectedInstallTime);

            using var agent = MockTracerAgent.Create(Output, useTelemetry: true);
            using var telemetry = new MockTelemetryAgent();
            EnableAgentlessTelemetry(telemetry.Port, enableDependencies: true);

            var httpPort = TcpPortProvider.GetOpenPort();

            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppStarted));

            var appStarted = telemetry.Telemetry.Should()
                .ContainSingle(x => x.IsRequestType(TelemetryRequestTypes.AppStarted))
                .Subject;

            var appStartedPayload = appStarted.TryGetPayload<AppStartedPayload>(TelemetryRequestTypes.AppStarted);

            appStartedPayload.Should().NotBeNull();
            appStartedPayload.InstallSignature.Should().NotBeNull()
                .And.BeEquivalentTo(new AppStartedPayload.InstallSignaturePayload
                {
                    InstallId = expectedInstallId,
                    InstallType = expectedInstallType,
                    InstallTime = expectedInstallTime
                });
        }

        private static void AssertService(MockTracerAgent mockAgent, string expectedServiceName, string expectedServiceVersion)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppStarted));
            AssertService(mockAgent.Telemetry.Cast<TelemetryData>(), expectedServiceName, expectedServiceVersion);
        }

        private static void AssertService(MockTelemetryAgent telemetry, string expectedServiceName, string expectedServiceVersion)
        {
            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppStarted));
            AssertService(telemetry.Telemetry, expectedServiceName, expectedServiceVersion);
        }

        private static void AssertService(IEnumerable<TelemetryData> allData, string expectedServiceName, string expectedServiceVersion)
        {
            var appClosing = allData.Should()
                                    .ContainSingle(x => x.IsRequestType(TelemetryRequestTypes.AppClosing))
                                    .Subject;
            appClosing.Application.ServiceName.Should().Be(expectedServiceName);
            appClosing.Application.ServiceVersion.Should().Be(expectedServiceVersion);
        }

        private static void AssertDependencies(MockTracerAgent mockAgent, bool? enableDependencies)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppClosing));
            AssertDependencies(mockAgent.Telemetry.Cast<TelemetryData>(), enableDependencies);
        }

        private static void AssertDependencies(MockTelemetryAgent telemetry, bool? enableDependencies)
        {
            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));
            AssertDependencies(telemetry.Telemetry, enableDependencies);
        }

        private static void AssertDependencies(IEnumerable<TelemetryData> allData, bool? enableDependencies)
        {
            var enabled = (enableDependencies ?? DependenciesEnabledDefault);

            var dependencies = allData
                         .Where(x => x.TryGetPayload<AppDependenciesLoadedPayload>(TelemetryRequestTypes.AppDependenciesLoaded) is { });

            if (enabled)
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

        private static void WaitForAllTelemetry(MockTracerAgent mockAgent)
            => mockAgent.WaitForLatestTelemetry(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppClosing));

        private static void WaitForAllTelemetry(MockTelemetryAgent telemetry)
            => telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

        private static void AssertNoRedactedErrorLogs(MockTracerAgent mockAgent)
        {
            WaitForAllTelemetry(mockAgent);
            AssertNoRedactedErrorLogs(mockAgent.Telemetry.Cast<TelemetryData>());
        }

        private static void AssertNoRedactedErrorLogs(MockTelemetryAgent telemetry)
        {
            WaitForAllTelemetry(telemetry);
            AssertNoRedactedErrorLogs(telemetry.Telemetry);
        }

        private static void AssertNoRedactedErrorLogs(IEnumerable<TelemetryData> allData)
            => allData.Where(x => x.IsRequestType(TelemetryRequestTypes.RedactedErrorLogs)).Should().BeEmpty();

        private void EnableAgentlessTelemetry(int standaloneAgentPort, bool? enableDependencies)
        {
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "true");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED", "true");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENT_PROXY_ENABLED", "false");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_URL", $"http://localhost:{standaloneAgentPort}");
            // API key is required for agentless
            SetEnvironmentVariable("DD_API_KEY", "INVALID_KEY_FOR_TESTS");
            EnableDependencies(enableDependencies);
            // Disable by default
            SetEnvironmentVariable(ConfigurationKeys.Telemetry.TelemetryLogsEnabled, "0");
        }

        private void EnableAgentProxyTelemetry(bool? enableDependencies)
        {
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "true");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED", "false");
            EnableDependencies(enableDependencies);
            // Disable by default for tests
            SetEnvironmentVariable(ConfigurationKeys.Telemetry.TelemetryLogsEnabled, "0");
        }

        private void EnableDependencies(bool? enableDependencies)
        {
            SetEnvironmentVariable(ConfigurationKeys.Telemetry.DependencyCollectionEnabled, (enableDependencies ?? DependenciesEnabledDefault).ToString());
        }
    }
}
