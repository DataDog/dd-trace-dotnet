// <copyright file="ProcessStartTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class ProcessStartTests : ProcessStartCommonTests
    {
        public ProcessStartTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTracesV0() => await RunTest("v0", nameof(ProcessStartTests), 5);

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTracesV1() => await RunTest("v1", nameof(ProcessStartTests), 5);

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public Task IntegrationDisabled() => IntegrationDisabledMethod();

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task DoesNotCollectWhenExplicitlySkipped()
        {
            // This _shouldn't_ make any difference, it's only if it's explicitly passed to the StartInfo
            SetEnvironmentVariable("DO_NOT_TRACE_PROCESS", "1");

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(1);

            // could snapshot this, but really doesn't seem worth it, as all we
            // really want to test is that we don't get _2_ spans
            spans.Should().ContainSingle(x => x.Name == "command_execution");
        }
    }
}
