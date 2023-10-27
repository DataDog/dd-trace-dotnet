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
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);

                await AssertExpectedSpans(spans);
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            telemetry.AssertConfiguration(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
            telemetry.AssertConfiguration(ConfigurationKeys.PropagationStyleExtract, "tracecontext,Datadog");
            telemetry.AssertConfiguration(ConfigurationKeys.PropagationStyleInject, "tracecontext,Datadog");

            AssertService(telemetry, "Samples.Telemetry", ServiceVersion);
            AssertDependencies(telemetry, enableDependencies);
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
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

                var spans = agent.WaitForSpans(ExpectedSpans);
                await AssertExpectedSpans(spans);
            }

            agent.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
            agent.AssertConfiguration(ConfigTelemetryData.NativeTracerVersion, TracerConstants.ThreePartVersion);
            agent.AssertConfiguration(ConfigurationKeys.PropagationStyleExtract, "tracecontext,Datadog");
            agent.AssertConfiguration(ConfigurationKeys.PropagationStyleInject, "tracecontext,Datadog");

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
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
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

        private static void AssertService(MockTracerAgent mockAgent, string expectedServiceName, string expectedServiceVersion)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryWrapper)x).IsRequestType(TelemetryRequestTypes.AppStarted));
            AssertService(mockAgent.Telemetry.Cast<TelemetryWrapper>(), expectedServiceName, expectedServiceVersion);
        }

        private static void AssertService(MockTelemetryAgent telemetry, string expectedServiceName, string expectedServiceVersion)
        {
            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppStarted));
            AssertService(telemetry.Telemetry, expectedServiceName, expectedServiceVersion);
        }

        private static void AssertService(IEnumerable<TelemetryWrapper> allData, string expectedServiceName, string expectedServiceVersion)
        {
            var appClosing = allData.Should()
                                    .ContainSingle(x => x.IsRequestType(TelemetryRequestTypes.AppClosing))
                                    .Subject;
            switch (appClosing)
            {
                case TelemetryWrapper.V2 v2:
                    v2.Data.Application.ServiceName.Should().Be(expectedServiceName);
                    v2.Data.Application.ServiceVersion.Should().Be(expectedServiceVersion);
                    break;
                default:
                    throw new InvalidOperationException("Unknown telemetry wrapper type: " + appClosing?.GetType());
            }
        }

        private static void AssertDependencies(MockTracerAgent mockAgent, bool? enableDependencies)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryWrapper)x).IsRequestType(TelemetryRequestTypes.AppClosing));
            AssertDependencies(mockAgent.Telemetry.Cast<TelemetryWrapper>(), enableDependencies);
        }

        private static void AssertDependencies(MockTelemetryAgent telemetry, bool? enableDependencies)
        {
            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));
            AssertDependencies(telemetry.Telemetry, enableDependencies);
        }

        private static void AssertDependencies(IEnumerable<TelemetryWrapper> allData, bool? enableDependencies)
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
