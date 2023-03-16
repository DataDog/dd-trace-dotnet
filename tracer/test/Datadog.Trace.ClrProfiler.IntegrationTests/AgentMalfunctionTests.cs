// <copyright file="AgentMalfunctionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
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
        private readonly ITestOutputHelper _testOutputHelper;

        private readonly Dictionary<TestTransports, MockTracerAgent> _agents = new();

        public AgentMalfunctionTests(ITestOutputHelper output, ITestOutputHelper testOutputHelper)
            : base("ProcessStart", output)
        {
            _testOutputHelper = testOutputHelper;
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [InlineData(AgentBehaviour.Normal, TestTransports.Tcp)]
        [InlineData(AgentBehaviour.NoAnswer, TestTransports.Tcp)]
        [InlineData(AgentBehaviour.WrongAnswer, TestTransports.Tcp)]
        [InlineData(AgentBehaviour.Return404, TestTransports.Tcp)]
        [InlineData(AgentBehaviour.Return500, TestTransports.Tcp)]
        [InlineData(AgentBehaviour.Normal, TestTransports.WindowsNamedPipe)]
        [InlineData(AgentBehaviour.NoAnswer, TestTransports.WindowsNamedPipe)]
        [InlineData(AgentBehaviour.WrongAnswer, TestTransports.WindowsNamedPipe)]
        [InlineData(AgentBehaviour.Return404, TestTransports.WindowsNamedPipe)]
        [InlineData(AgentBehaviour.Return500, TestTransports.WindowsNamedPipe)]
#if NETCOREAPP3_1_OR_GREATER
        [InlineData(AgentBehaviour.Normal, TestTransports.Uds)]
        [InlineData(AgentBehaviour.NoAnswer, TestTransports.Uds)]
        [InlineData(AgentBehaviour.WrongAnswer, TestTransports.Uds)]
        [InlineData(AgentBehaviour.Return404, TestTransports.Uds)]
        [InlineData(AgentBehaviour.Return500, TestTransports.Uds)]
#endif
        public void SubmitsTraces(AgentBehaviour behaviour, TestTransports transportType)
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            if (transportType == TestTransports.WindowsNamedPipe && !EnvironmentTools.IsWindows())
            {
                throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
            }

            try
            {
                if (!_agents.TryGetValue(transportType, out var agent))
                {
                    agent = EnvironmentHelper.GetMockAgent();
                    _agents.Add(transportType, agent);
                }

                EnvironmentHelper.EnableTransport(transportType);
                agent.SetBehaviour(behaviour);
                TestInstrumentation(agent);
            }
            catch (Exception)
            {
                _testOutputHelper.WriteLine("Error while executing test. Disposing the agent");
                _agents[transportType].Dispose();
                _agents.Remove(transportType);
                throw;
            }
        }

        private async void TestInstrumentation(MockTracerAgent agent)
        {
            const int expectedSpanCount = 5;
            const string expectedOperationName = "command_execution";

            using var process = RunSampleAndWaitForExit(agent);

            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName, timeoutInMilliseconds: 40000);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(StackRegex, string.Empty);
            settings.AddRegexScrubber(ErrorMsgRegex, string.Empty);
            var filename = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
                "ProcessStartTests.SubmitsTracesLinux" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                    "ProcessStartTests.SubmitsTracesOsx" :
                    "ProcessStartTests.SubmitsTraces";
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();

            VerifyInstrumentation(process.Process);
        }
    }
}
