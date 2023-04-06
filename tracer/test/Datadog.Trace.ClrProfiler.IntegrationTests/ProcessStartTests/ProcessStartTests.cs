// <copyright file="ProcessStartTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
    public class ProcessStartTests : TracingIntegrationTest
    {
        private const string ServiceName = "Samples.ProcessStart";
        private static readonly Regex StackRegex = new(@"      error.stack:(\n|\r){1,2}.*(\n|\r){1,2}.*,(\r|\n){1,2}");
        private static readonly Regex ErrorMsgRegex = new(@"      error.msg:.*,(\r|\n){1,2}");

        public ProcessStartTests(ITestOutputHelper output)
            : base("ProcessStart", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsProcess();

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTracesV0() => await RunTest("v0");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTracesV1() => await RunTest("v1");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void IntegrationDisabled()
        {
            const int totalSpanCount = 5;
            const string expectedOperationName = "command_execution";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.Process)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.Process);
        }

        private async Task RunTest(string metadataSchemaVersion)
        {
            const int expectedSpanCount = 5;
            const string expectedOperationName = "command_execution";

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{ServiceName}-command" : ServiceName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            ValidateIntegrationSpans(spans, expectedServiceName: clientSpanServiceName, isExternalSpan);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(StackRegex, string.Empty);
            settings.AddRegexScrubber(ErrorMsgRegex, string.Empty);
            var filename = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
                "ProcessStartTests.SubmitsTracesLinux" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                    "ProcessStartTests.SubmitsTracesOsx" :
                    "ProcessStartTests.SubmitsTraces";

            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename + $".Schema{metadataSchemaVersion.ToUpper()}")
                              .DisableRequireUniquePrefix();

            VerifyInstrumentation(process.Process);

            telemetry.AssertIntegrationEnabled(IntegrationId.Process);
        }
    }
}
