// <copyright file="ProcessStartCommonTests.cs" company="Datadog">
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
    public class ProcessStartCommonTests : TracingIntegrationTest
    {
        private static readonly Regex StackRegex = new(@"      error.stack:(\n|\r){1,2}.*(\n|\r){1,2}.*,(\r|\n){1,2}");
        private static readonly Regex ErrorMsgRegex = new(@"      error.msg:.*,(\r|\n){1,2}");
        private static readonly Regex ExitCodeRegex = new(@"cmd\.exit_code: (\d+),", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ProcessStartCommonTests(ITestOutputHelper output)
            : base("ProcessStart", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsProcess(metadataSchemaVersion);

        protected void IntegrationDisabledMethod()
        {
            const int totalSpanCount = 5;
            const string expectedOperationName = "command_execution";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.Process)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.Spans; // no spans expected

            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.Process);
        }

        protected async Task RunTest(string metadataSchemaVersion, string testName, bool collectCommands = false)
        {
            // 3 on non-windows because of SecureString
            var expectedSpanCount = EnvironmentTools.IsWindows() ? 5 : 3;

            const string expectedOperationName = "command_execution";

            if (collectCommands)
            {
                SetEnvironmentVariable("DD_COMMANDS_COLLECTION_ENABLED", "true");
            }

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-command" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();

            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(ExitCodeRegex, string.Empty);
            settings.AddRegexScrubber(StackRegex, string.Empty);
            settings.AddRegexScrubber(ErrorMsgRegex, string.Empty);
            var filename = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
                $"{testName}.SubmitsTracesLinux" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                    $"{testName}.SubmitsTracesOsx" :
                    $"{testName}.SubmitsTraces";

            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename + $".Schema{metadataSchemaVersion.ToUpper()}")
                              .DisableRequireUniquePrefix();

            VerifyInstrumentation(process.Process);

            telemetry.AssertIntegrationEnabled(IntegrationId.Process);
        }
    }
}
