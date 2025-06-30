// <copyright file="AgentMalfunctionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
    public class AgentMalfunctionTests : TestHelper
    {
        private static readonly Regex StackRegex = new(@"      error.stack:(\n|\r){1,2}.*(\n|\r){1,2}.*,(\r|\n){1,2}");
        private static readonly Regex ErrorMsgRegex = new(@"      error.msg:.*,(\r|\n){1,2}");
        private readonly ITestOutputHelper _output;

        public AgentMalfunctionTests(ITestOutputHelper output)
            : base("ProcessStart", output)
        {
            _output = output;
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> TestData
            => from behaviour in (AgentBehaviour[])Enum.GetValues(typeof(AgentBehaviour))
               from metadataSchemaVersion in new[] { "v0", "v1" }
               from dataPipelineEnabled in new[] { false } // TODO: re-enable datapipeline tests - Currently it causes too much flake with duplicate spans
               select new object[] { behaviour, metadataSchemaVersion, dataPipelineEnabled };

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Flaky("Named pipes is flaky", maxRetries: 3)]
        public Task NamedPipes_SubmitsTraces(AgentBehaviour behaviour, string metadataSchemaVersion, bool dataPipelineEnabled)
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);
            return SubmitsTraces(behaviour, TestTransports.WindowsNamedPipe, metadataSchemaVersion, dataPipelineEnabled);
        }

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public Task Tcp_SubmitsTraces(AgentBehaviour behaviour, string metadataSchemaVersion, bool dataPipelineEnabled)
            => SubmitsTraces(behaviour, TestTransports.Tcp, metadataSchemaVersion, dataPipelineEnabled);

#if NETCOREAPP3_1_OR_GREATER
        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public Task Uds_SubmitsTraces(AgentBehaviour behaviour, string metadataSchemaVersion, bool dataPipelineEnabled)
            => SubmitsTraces(behaviour, TestTransports.Uds, metadataSchemaVersion, dataPipelineEnabled);
#endif

        private async Task SubmitsTraces(AgentBehaviour behaviour, TestTransports transportType, string metadataSchemaVersion, bool dataPipelineEnabled)
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            var canUseStatsD = EnvironmentHelper.CanUseStatsD(transportType);
            if (!canUseStatsD)
            {
                SetEnvironmentVariable(ConfigurationKeys.RuntimeMetricsEnabled, "0");
            }

            EnvironmentHelper.EnableTransport(transportType);
            SetEnvironmentVariable(ConfigurationKeys.TraceDataPipelineEnabled, dataPipelineEnabled.ToString());

            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: canUseStatsD);
            var customResponse = behaviour switch
            {
                AgentBehaviour.Return404 => new MockTracerResponse { StatusCode = 404 },
                AgentBehaviour.Return500 => new MockTracerResponse { StatusCode = 500 },
                AgentBehaviour.WrongAnswer => new MockTracerResponse("WRONG_ANSWER"),
                AgentBehaviour.NoAnswer => new MockTracerResponse { SendResponse = false },
                _ => null,
            };

            if (customResponse is { } cr)
            {
                // set everything except traces, but only these are actually used
                agent.CustomResponses[MockTracerResponseType.Telemetry] = cr;
                agent.CustomResponses[MockTracerResponseType.Info] = cr;
                agent.CustomResponses[MockTracerResponseType.RemoteConfig] = cr;
            }

            // 3 on non-windows because of SecureString
            var expectedSpanCount = EnvironmentTools.IsWindows() ? 5 : 3;

            const string expectedOperationName = "command_execution";

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

            using var process = await RunSampleAndWaitForExit(agent);

            var spans = await agent.WaitForSpansAsync(expectedSpanCount, operationName: expectedOperationName, timeoutInMilliseconds: 40000);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(StackRegex, string.Empty);
            settings.AddRegexScrubber(ErrorMsgRegex, string.Empty);
            var filename = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
                "ProcessStartTests.SubmitsTracesLinux" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                    "ProcessStartTests.SubmitsTracesOsx" :
                    "ProcessStartTests.SubmitsTraces";

            settings.AddSimpleScrubber($"LD_PRELOAD={EnvironmentHelper.GetApiWrapperPath()}", "LD_PRELOAD=path");

            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename + $".Schema{metadataSchemaVersion.ToUpper()}")
                              .DisableRequireUniquePrefix();

            VerifyInstrumentation(process.Process);
        }
    }
}
