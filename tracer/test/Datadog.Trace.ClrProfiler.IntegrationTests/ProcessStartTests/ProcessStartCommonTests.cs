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

        public ProcessStartCommonTests(ITestOutputHelper output)
            : base("ProcessStart", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsProcess(metadataSchemaVersion);

        protected async Task IntegrationDisabledMethod()
        {
            const string expectedOperationName = "command_execution";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.Process)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent);
            var spans = agent.Spans; // no spans expected

            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.Process);
        }

        protected async Task RunTest(string metadataSchemaVersion, string testName, int expectedSpanCount, bool collectCommands = false)
        {
            // expectedSpanCount when 10
            // Windows .NET Framework/.NET Core 3.0 and lower: 6 spans
            // Windows .NET Core 3.1+: 10 spans
            // Linux/OSX .NET Core 3.0 and lower: 6 spans
            // Linux/OSX .NET Core 3.1+: 10 spans

            // expectedSpanCount when 5
            // Windows will have 5 spans for all
            // Linux/OSX will have 3 spans
#if !NETCOREAPP3_1_OR_GREATER
            // Windows/Linux/Mac all expect 6 for .NET Framework or .NET Core 3.0 and below
            if (expectedSpanCount >= 10)
            {
                expectedSpanCount = 6;
            }
#endif

            if (expectedSpanCount == 5 && !EnvironmentTools.IsWindows())
            {
                expectedSpanCount = 3;
            }

            const string expectedOperationName = "command_execution";

            if (collectCommands)
            {
                SetEnvironmentVariable("DD_TRACE_COMMANDS_COLLECTION_ENABLED", "true");
            }

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-command" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();

            using var process = await RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
            ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(StackRegex, string.Empty);
            settings.AddRegexScrubber(ErrorMsgRegex, string.Empty);
            var filename = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
                $"{testName}.SubmitsTracesLinux" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                    $"{testName}.SubmitsTracesOsx" :
                    $"{testName}.SubmitsTraces";

            settings.AddSimpleScrubber($"LD_PRELOAD={EnvironmentHelper.GetApiWrapperPath()}", "LD_PRELOAD=path");

            if (collectCommands)
            {
                // Make sure the PATH name is the same in span for all OS
                settings.AddSimpleScrubber("PATH=testPath", "Path=testPath");

#if !NETCOREAPP3_1_OR_GREATER
                // The collect command will have different spans depending of the dotnet version
                // ArgumentList is not available in .NET Core <=2.0 and .NET Framework
                // and some tests are performed on ArgumentList
                filename += ".netfxOrNetCore2";
#endif
            }

            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename + $".Schema{metadataSchemaVersion.ToUpper()}")
                              .DisableRequireUniquePrefix();

            VerifyInstrumentation(process.Process);

            telemetry.AssertIntegrationEnabled(IntegrationId.Process);
        }
    }
}
