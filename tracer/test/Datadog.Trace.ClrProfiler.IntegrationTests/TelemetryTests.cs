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

                var spans = await agent.WaitForSpansAsync(ExpectedSpans);

                await AssertExpectedSpans(spans);
            }

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.HttpMessageHandler);
            await telemetry.AssertConfigurationAsync(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
            await telemetry.AssertConfigurationAsync(ConfigurationKeys.PropagationStyleExtract, "Datadog,tracecontext,baggage");
            await telemetry.AssertConfigurationAsync(ConfigurationKeys.PropagationStyleInject, "Datadog,tracecontext,baggage");

            await AssertServiceAsync(telemetry, "Samples.Telemetry", ServiceVersion);
            await AssertDependenciesAsync(telemetry, enableDependencies);
            await AssertNoRedactedErrorLogsAsync(telemetry);
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

                var spans = await agent.WaitForSpansAsync(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            await agent.AssertIntegrationEnabledAsync(IntegrationId.HttpMessageHandler);
            await agent.AssertConfigurationAsync(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
            await agent.AssertConfigurationAsync(ConfigurationKeys.PropagationStyleExtract, "Datadog,tracecontext,baggage");
            await agent.AssertConfigurationAsync(ConfigurationKeys.PropagationStyleInject, "Datadog,tracecontext,baggage");

            await AssertServiceAsync(agent, "Samples.Telemetry", ServiceVersion);
            await AssertDependenciesAsync(agent, enableDependencies);
            await AssertNoRedactedErrorLogsAsync(agent);
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

                var spans = await agent.WaitForSpansAsync(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            // Shouldn't have any, but wait for 5s
            await agent.WaitForLatestTelemetryAsync(x => true);
            agent.Telemetry.Should().BeEmpty();
            await AssertNoRedactedErrorLogsAsync(agent);
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        [MemberData(nameof(Data))]
        [Flaky("Named pipes is flaky", maxRetries: 3)]
        public async Task WhenUsingNamedPipesAgent_UsesNamedPipesTelemetry(bool? enableDependencies)
        {
            if (!EnvironmentTools.IsWindows())
            {
                throw new SkipException("WindowsNamedPipe transport is only supported on Windows");
            }

            EnvironmentHelper.EnableWindowsNamedPipes();
            EnableAgentProxyTelemetry(enableDependencies);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            agent.Output = Output;

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = await agent.WaitForSpansAsync(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            await agent.AssertIntegrationEnabledAsync(IntegrationId.HttpMessageHandler);
            await agent.AssertConfigurationAsync(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
            await AssertServiceAsync(agent, "Samples.Telemetry", ServiceVersion);
            await AssertDependenciesAsync(agent, enableDependencies);
            await AssertNoRedactedErrorLogsAsync(agent);
        }

#if NETCOREAPP3_1_OR_GREATER
        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
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

                var spans = await agent.WaitForSpansAsync(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            await agent.AssertIntegrationEnabledAsync(IntegrationId.HttpMessageHandler);
            await AssertServiceAsync(agent, "Samples.Telemetry", ServiceVersion);
            await AssertDependenciesAsync(agent, enableDependencies);
            await AssertNoRedactedErrorLogsAsync(agent);
        }
#endif

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task Telemetry_SendsMetrics()
        {
            // telemetry metric events under test are sent only when using managed trace exporter
            SetEnvironmentVariable(ConfigurationKeys.TraceDataPipelineEnabled, "false");

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

                var spans = await agent.WaitForSpansAsync(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            // The numbers here may change, but we should have _some_
            (await telemetry.GetDistributionsAsync(DistributionShared.InitTime.GetName())).Sum(x => x.Points.Count).Should().BeGreaterThan(4);

            (await telemetry.GetMetricDataPointsAsync(Count.TraceChunkEnqueued.GetName())).Sum(x => x.Value).Should().Be(ExpectedTraces);

            // The exact number of logs aren't important, but we should have some
            (await telemetry.GetMetricDataPointsAsync(Count.LogCreated.GetName(), "level:info"))
                     .Sum(x => x.Value).Should().BeGreaterThan(10);

            // Should have at least 1 call to the Agentless telemetry API and no errors
            var telemetryRequests = (await telemetry.GetMetricDataPointsAsync(Count.TelemetryApiRequests.GetName(), "endpoint:agentless"))
                                             .Sum(x => x.Value);
            telemetryRequests.Should().BeGreaterThan(0);
            (await telemetry.GetMetricDataPointsAsync(Count.TelemetryApiResponses.GetName(), "endpoint:agentless"))
                     .Sum(x => x.Value).Should().Be(telemetryRequests);
            (await telemetry.GetMetricDataPointsAsync(Count.TelemetryApiRequests.GetName(), "endpoint:agent")).Should().BeEmpty();
            (await telemetry.GetMetricDataPointsAsync(Count.TelemetryApiErrors.GetName())).Should().BeEmpty();

            // we inject and extract headers once
            // avoiding checking the specific tag + count here so this doesn't need updating if we change the defaults
            (await telemetry.GetMetricDataPointsAsync(Count.ContextHeaderStyleInjected.GetName()))
                     .Should()
                     .NotBeEmpty()
                     .And.OnlyContain(x => x.Tags.Length > 0)
                     .And.Subject.Select(x => string.Join(",", x.Tags))
                     .Distinct()
                     .Should()
                     .HaveCountGreaterOrEqualTo(1);

            // hopefully no errors
            (await telemetry.GetMetricDataPointsAsync(CountShared.IntegrationsError.GetName())).Should().BeEmpty();
            (await telemetry.GetMetricDataPointsAsync(Count.VersionConflictTracerCreated.GetName())).Should().BeEmpty();

            (await telemetry.GetMetricDataPointsAsync(Gauge.Instrumentations.GetName())).Sum(x => x.Value).Should().BeGreaterThan(1);

            (await telemetry.GetMetricDataPointsAsync(Count.SpanCreated.GetName())).Sum(x => x.Value).Should().Be(ExpectedSpans);
            (await telemetry.GetMetricDataPointsAsync(Count.SpanFinished.GetName())).Sum(x => x.Value).Should().Be(ExpectedSpans);
            (await telemetry.GetMetricDataPointsAsync(Count.SpanEnqueuedForSerialization.GetName())).Sum(x => x.Value).Should().Be(ExpectedSpans);
            (await telemetry.GetMetricDataPointsAsync(Count.TraceSegmentCreated.GetName())).Sum(x => x.Value).Should().Be(ExpectedTraces);
            (await telemetry.GetMetricDataPointsAsync(Count.TraceChunkEnqueued.GetName())).Sum(x => x.Value).Should().Be(ExpectedTraces);
            (await telemetry.GetMetricDataPointsAsync(Count.TraceChunkSent.GetName())).Sum(x => x.Value).Should().Be(ExpectedTraces);

            (await telemetry.GetMetricDataPointsAsync(Count.TraceApiRequests.GetName())).Sum(x => x.Value).Should().BeGreaterThan(0);
            (await telemetry.GetMetricDataPointsAsync(Count.TraceApiResponses.GetName())).Sum(x => x.Value).Should().BeGreaterThan(0);
            (await telemetry.GetMetricDataPointsAsync(Count.TraceApiErrors.GetName())).Should().BeEmpty();
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
            SetEnvironmentVariable("SEND_ERROR_LOG", "1");

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = await agent.WaitForSpansAsync(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            await WaitForAllTelemetryAsync(telemetry);
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
                   .Be("Sending an error log using hacky reflection");
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

                var spans = await agent.WaitForSpansAsync(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            await telemetry.WaitForLatestTelemetryAsync(x => x.IsRequestType(TelemetryRequestTypes.AppStarted));

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

        private static async Task AssertServiceAsync(MockTracerAgent mockAgent, string expectedServiceName, string expectedServiceVersion)
        {
            await mockAgent.WaitForLatestTelemetryAsync(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppStarted));
            AssertService(mockAgent.Telemetry.Cast<TelemetryData>(), expectedServiceName, expectedServiceVersion);
        }

        private static async Task AssertServiceAsync(MockTelemetryAgent telemetry, string expectedServiceName, string expectedServiceVersion)
        {
            await telemetry.WaitForLatestTelemetryAsync(x => x.IsRequestType(TelemetryRequestTypes.AppStarted));
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

        private static async Task AssertDependenciesAsync(MockTracerAgent mockAgent, bool? enableDependencies)
        {
            await mockAgent.WaitForLatestTelemetryAsync(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppClosing));
            AssertDependencies(mockAgent.Telemetry.Cast<TelemetryData>(), enableDependencies);
        }

        private static async Task AssertDependenciesAsync(MockTelemetryAgent telemetry, bool? enableDependencies)
        {
            await telemetry.WaitForLatestTelemetryAsync(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));
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

        private static Task<object> WaitForAllTelemetryAsync(MockTracerAgent mockAgent)
            => mockAgent.WaitForLatestTelemetryAsync(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppClosing));

        private static Task<TelemetryData> WaitForAllTelemetryAsync(MockTelemetryAgent telemetry)
            => telemetry.WaitForLatestTelemetryAsync(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

        private static async Task AssertNoRedactedErrorLogsAsync(MockTracerAgent mockAgent)
        {
            await WaitForAllTelemetryAsync(mockAgent);
            AssertNoRedactedErrorLogs(mockAgent.Telemetry.Cast<TelemetryData>());
        }

        private static async Task AssertNoRedactedErrorLogsAsync(MockTelemetryAgent telemetry)
        {
            await WaitForAllTelemetryAsync(telemetry);
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
