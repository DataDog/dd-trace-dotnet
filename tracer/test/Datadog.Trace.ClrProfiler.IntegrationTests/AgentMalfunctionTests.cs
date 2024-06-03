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
        private static readonly TestTransports[] Transports = new[]
        {
            TestTransports.Tcp,
            TestTransports.WindowsNamedPipe,
#if NETCOREAPP3_1_OR_GREATER
            TestTransports.Uds,
#endif
        };

        private readonly ITestOutputHelper _output;

        public AgentMalfunctionTests(ITestOutputHelper output)
            : base("ProcessStart", output)
        {
            _output = output;
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> TestData
            => from behaviour in (AgentBehaviour[])Enum.GetValues(typeof(AgentBehaviour))
               from transportType in Transports
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new object[] { behaviour, transportType, metadataSchemaVersion };

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces(AgentBehaviour behaviour, TestTransports transportType, string metadataSchemaVersion)
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            if (transportType == TestTransports.WindowsNamedPipe && !EnvironmentTools.IsWindows())
            {
                throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
            }

            EnvironmentHelper.EnableTransport(transportType);
            using var agent = EnvironmentHelper.GetMockAgent();
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

            // The server implementation of named pipes is flaky so have 3 attempts
            var attemptsRemaining = 3;
            while (true)
            {
                try
                {
                    attemptsRemaining--;
                    await TestInstrumentation(agent, metadataSchemaVersion);
                    return;
                }
                catch (Exception ex) when (transportType == TestTransports.WindowsNamedPipe && attemptsRemaining > 0 && ex is not SkipException)
                {
                    await ReportRetry(_output, attemptsRemaining, ex);
                }
            }
        }

        private async Task TestInstrumentation(MockTracerAgent agent, string metadataSchemaVersion)
        {
            // 3 on non-windows because of SecureString
            var expectedSpanCount = EnvironmentTools.IsWindows() ? 5 : 3;

            const string expectedOperationName = "command_execution";

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

            using var process = await RunSampleAndWaitForExit(agent);

            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName, timeoutInMilliseconds: 40000);

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
