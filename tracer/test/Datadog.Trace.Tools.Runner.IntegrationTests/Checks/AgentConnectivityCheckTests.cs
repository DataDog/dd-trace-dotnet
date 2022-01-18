// <copyright file="AgentConnectivityCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.Runner.Checks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using static Datadog.Trace.Tools.Runner.Checks.Resources;

namespace Datadog.Trace.Tools.Runner.IntegrationTests.Checks
{
    [Collection(nameof(ConsoleTestsCollection))]
    public class AgentConnectivityCheckTests : ConsoleTestHelper
    {
        public AgentConnectivityCheckTests(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<object[]> TestData => new List<object[]>
        {
            new object[] { new[] { ("DD_TRACE_AGENT_URL", "http://fakeurl:7777/") } },
            new object[] { new[] { ("DD_AGENT_HOST", "fakeurl"), ("DD_TRACE_AGENT_PORT", "7777") } },
            new object[] { new[] { ("DD_AGENT_HOST", "wrong"), ("DD_TRACE_AGENT_PORT", "1111"), ("DD_TRACE_AGENT_URL", "http://fakeurl:7777/") } },
        };

        [Theory]
        [MemberData(nameof(TestData))]
        public async Task DetectAgentUrl((string, string)[] environmentVariables)
        {
            using var helper = await StartConsole(enableProfiler: false, environmentVariables);

            using var console = ConsoleHelper.Redirect();

            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            var result = await AgentConnectivityCheck.Run(processInfo);

            console.Output.Should().Contain(DetectedAgentUrlFormat("http://fakeurl:7777/"));
        }

        [Fact]
        public async Task NoAgent()
        {
            using var console = ConsoleHelper.Redirect();

            var result = await AgentConnectivityCheck.Run("http://fakeurl/");

            result.Should().BeFalse();

            // Note for future maintainers: this assertion needs to be changed to something smarter
            // if the error message stops being at the end of the string
            console.Output.Should().Contain(ErrorDetectingAgent("http://fakeurl/", string.Empty));
        }

        [Fact]
        public async Task FaultyAgent()
        {
            using var console = ConsoleHelper.Redirect();

            using var agent = new MockTracerAgent(TcpPortProvider.GetOpenPort());

            agent.RequestReceived += (s, e) => e.Value.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var result = await AgentConnectivityCheck.Run($"http://localhost:{agent.Port}/");

            result.Should().BeFalse();

            console.Output.Should().Contain(WrongStatusCodeFormat(HttpStatusCode.InternalServerError));
        }

        [Fact]
        public async Task DetectVersion()
        {
            const string expectedVersion = "7.66.55";

            using var console = ConsoleHelper.Redirect();

            using var agent = new MockTracerAgent(TcpPortProvider.GetOpenPort())
            {
                Version = expectedVersion
            };

            var result = await AgentConnectivityCheck.Run($"http://localhost:{agent.Port}/");

            result.Should().BeTrue();

            console.Output.Should().Contain(DetectedAgentUrlFormat(expectedVersion));
        }

        [Fact]
        public async Task NoVersion()
        {
            using var console = ConsoleHelper.Redirect();

            using var agent = new MockTracerAgent(TcpPortProvider.GetOpenPort())
            {
                Version = null
            };

            var result = await AgentConnectivityCheck.Run($"http://localhost:{agent.Port}/");

            result.Should().BeTrue();

            console.Output.Should().Contain(AgentDetectionFailed);
        }
    }
}
