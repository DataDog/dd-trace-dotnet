// <copyright file="OpenTracingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class OpenTracingTests : TracingIntegrationTest
    {
        public OpenTracingTests(ITestOutputHelper output)
            : base("OpenTracing", output)
        {
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => Result.DefaultSuccess;

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task MinimalSpan()
        {
            using var telemetry = this.ConfigureTelemetry();
            using var agent = MockTracerAgent.Create(Output);
            using (await RunSampleAndWaitForExit(agent, arguments: nameof(MinimalSpan)))
            {
                const int expectedSpanCount = 1;
                var spans = agent.WaitForSpans(expectedSpanCount);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings);
                telemetry.AssertIntegrationEnabled(IntegrationId.DatadogTraceManual);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task CustomServiceName()
        {
            using var telemetry = this.ConfigureTelemetry();
            using var agent = MockTracerAgent.Create(Output);
            using (await RunSampleAndWaitForExit(agent, arguments: nameof(CustomServiceName)))
            {
                const int expectedSpanCount = 1;
                var spans = agent.WaitForSpans(expectedSpanCount);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings);
                telemetry.AssertIntegrationEnabled(IntegrationId.DatadogTraceManual);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task Utf8Everywhere()
        {
            using var telemetry = this.ConfigureTelemetry();
            using var agent = MockTracerAgent.Create(Output);
            using (await RunSampleAndWaitForExit(agent, arguments: nameof(Utf8Everywhere)))
            {
                const int expectedSpanCount = 1;
                var spans = agent.WaitForSpans(expectedSpanCount);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings);
                telemetry.AssertIntegrationEnabled(IntegrationId.DatadogTraceManual);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task OpenTracingSpanBuilderTests()
        {
            const string scenario = nameof(OpenTracingSpanBuilderTests);

            SetServiceName(nameof(OpenTracingSpanBuilderTests));

            using var telemetry = this.ConfigureTelemetry();
            using var agent = MockTracerAgent.Create(Output);
            using var result = await RunSampleAndWaitForExit(agent, arguments: scenario);
            result.ExitCode.Should().Be(0);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task OpenTracingSpanTests()
        {
            const string scenario = nameof(OpenTracingSpanTests);
            SetServiceName(nameof(OpenTracingSpanTests));

            using var telemetry = this.ConfigureTelemetry();
            using var agent = MockTracerAgent.Create(Output);
            using var result = await RunSampleAndWaitForExit(agent, arguments: scenario);
            result.ExitCode.Should().Be(0);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task HttpHeaderCodecTests()
        {
            const string scenario = nameof(HttpHeaderCodecTests);
            SetServiceName(nameof(HttpHeaderCodecTests));

            using var telemetry = this.ConfigureTelemetry();
            using var agent = MockTracerAgent.Create(Output);
            using var result = await RunSampleAndWaitForExit(agent, arguments: scenario);
            result.ExitCode.Should().Be(0);
        }
    }
}
