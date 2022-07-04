// <copyright file="AgentConnectivityCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.Runner.Checks;
using FluentAssertions;
using FluentAssertions.Execution;
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

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        public async Task DetectAgentUrl((string, string)[] environmentVariables)
        {
            using var helper = await StartConsole(enableProfiler: false, environmentVariables);

            using var console = ConsoleHelper.Redirect();

            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            _ = await AgentConnectivityCheck.RunAsync(processInfo!);

            console.Output.Should().Contain(DetectedAgentUrlFormat("http://fakeurl:7777/"));
        }

        [SkippableFact]
        public async Task DetectTransportHttp()
        {
            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort());

            var url = $"http://127.0.0.1:{agent.Port}/";

            using var helper = await StartConsole(enableProfiler: false, ("DD_TRACE_AGENT_URL", url));

            using var console = ConsoleHelper.Redirect();

            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            _ = await AgentConnectivityCheck.RunAsync(processInfo!);

            console.Output.Should().Contain(ConnectToEndpointFormat(url, "HTTP"));
        }

#if NETCOREAPP3_1_OR_GREATER
        [SkippableFact]
        public async Task DetectTransportUds()
        {
            var tracesUdsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var url = $"unix://{tracesUdsPath}";
            var uri = new System.Uri(url);

            using var agent = MockTracerAgent.Create(new UnixDomainSocketConfig(tracesUdsPath, null));
            using var helper = await StartConsole(enableProfiler: false, ("DD_TRACE_AGENT_URL", url));
            using var console = ConsoleHelper.Redirect();

            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            _ = await AgentConnectivityCheck.RunAsync(processInfo!);

            console.Output.Should().Contain(ConnectToEndpointFormat(uri.PathAndQuery, "domain sockets"));
        }
#endif

        [SkippableFact]
        public async Task NoAgent()
        {
            using var console = ConsoleHelper.Redirect();

            var result = await AgentConnectivityCheck.RunAsync(CreateSettings("http://fakeurl/"));

            result.Should().BeFalse();

            // Note for future maintainers: this assertion needs to be changed to something smarter
            // if the error message stops being at the end of the string
            console.Output.Should().Contain(ErrorDetectingAgent("http://fakeurl/", string.Empty));
        }

        [SkippableFact]
        public async Task FaultyAgent()
        {
            using var console = ConsoleHelper.Redirect();

            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort());

            agent.RequestReceived += (_, e) => e.Value.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var result = await AgentConnectivityCheck.RunAsync(CreateSettings($"http://localhost:{agent.Port}/"));

            result.Should().BeFalse();

            console.Output.Should().Contain(WrongStatusCodeFormat(HttpStatusCode.InternalServerError));
        }

        [SkippableFact]
        public async Task DetectVersion()
        {
            const string expectedVersion = "7.66.55";

            using var console = ConsoleHelper.Redirect();

            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort());
            agent.Version = expectedVersion;

            var result = await AgentConnectivityCheck.RunAsync(CreateSettings($"http://localhost:{agent.Port}/"));

            result.Should().BeTrue();

            console.Output.Should().Contain(DetectedAgentVersionFormat(expectedVersion));
        }

#if NETCOREAPP3_1_OR_GREATER
        [SkippableFact]
        public async Task DetectVersionUds()
        {
            const string expectedVersion = "7.66.55";

            using var console = ConsoleHelper.Redirect();

            var tracesUdsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            using var agent = MockTracerAgent.Create(new UnixDomainSocketConfig(tracesUdsPath, null));
            agent.Version = expectedVersion;

            var settings = new ExporterSettings
            {
                AgentUri = new System.Uri($"unix://{tracesUdsPath}"),
            };

            var result = await AgentConnectivityCheck.RunAsync(new ImmutableExporterSettings(settings));

            result.Should().BeTrue();

            console.Output.Should().Contain(DetectedAgentVersionFormat(expectedVersion));
        }
#endif

        [SkippableFact]
        public async Task NoVersion()
        {
            using var console = ConsoleHelper.Redirect();

            using var agent = MockTracerAgent.Create(TcpPortProvider.GetOpenPort());
            agent.Version = null;

            var result = await AgentConnectivityCheck.RunAsync(CreateSettings($"http://localhost:{agent.Port}/"));

            result.Should().BeTrue();

            console.Output.Should().Contain(AgentDetectionFailed);
        }

        private static ImmutableExporterSettings CreateSettings(string url) => new(new ExporterSettings { AgentUri = new(url) });
    }
}
