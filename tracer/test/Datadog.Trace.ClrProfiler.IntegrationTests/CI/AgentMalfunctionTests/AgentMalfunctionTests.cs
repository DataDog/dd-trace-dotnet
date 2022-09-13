// <copyright file="AgentMalfunctionTests.cs" company="Datadog">
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
    public class AgentMalfunctionTests : TestHelper
    {
        private static readonly Regex StackRegex = new(@"      error.stack:(\n|\r){1,2}.*(\n|\r){1,2}.*,(\r|\n){1,2}");
        private static readonly Regex ErrorMsgRegex = new(@"      error.msg:.*,(\r|\n){1,2}");

        public AgentMalfunctionTests(ITestOutputHelper output)
            : base("ProcessStart", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTracesWhenAgentDoesNotAnswer()
        {
            using var agent = EnvironmentHelper.GetMockAgent();
            agent.SetBehaviour(AgentBehaviour.NO_ANSWER);
            TestInstrumentation(agent);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTracesWhenAgentAnswersSlowly()
        {
            using var agent = EnvironmentHelper.GetMockAgent();
            agent.SetBehaviour(AgentBehaviour.SLOW_ANSWER);
            TestInstrumentation(agent);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTracesWhenAgentSendsWrongMessages()
        {
            using var agent = EnvironmentHelper.GetMockAgent();
            agent.SetBehaviour(AgentBehaviour.WRONG_ANSWER);
            TestInstrumentation(agent);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTracesWhenAgentReturns404()
        {
            using var agent = EnvironmentHelper.GetMockAgent();
            agent.SetBehaviour(AgentBehaviour.RETURN_404);
            TestInstrumentation(agent);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTracesWhenAgentReturns500()
        {
            using var agent = EnvironmentHelper.GetMockAgent();
            agent.SetBehaviour(AgentBehaviour.RETURN_500);
            TestInstrumentation(agent);
        }

        private async void TestInstrumentation(MockTracerAgent agent)
        {
            const int expectedSpanCount = 5;
            const string expectedOperationName = "command_execution";

            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(StackRegex, string.Empty);
            settings.AddRegexScrubber(ErrorMsgRegex, string.Empty);
            var filename = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "ProcessStartTests.SubmitsTracesLinux" : "ProcessStartTests.SubmitsTraces";
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();

            VerifyInstrumentation(process.Process);
        }
    }
}
