// <copyright file="AgentConnectivityCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using Datadog.Trace.Tools.Shared;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using static Datadog.Trace.Tools.dd_dotnet.Checks.Resources;

namespace Datadog.Trace.Tools.dd_dotnet.IntegrationTests.Checks;

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
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(TestData))]
    public async Task DetectAgentUrl((string, string)[] environmentVariables)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        using var helper = await StartConsole(enableProfiler: false, environmentVariables);

        using var console = ConsoleHelper.Redirect();

        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        _ = await AgentConnectivityCheck.RunAsync(processInfo.ExtractConfigurationSource(null, null));

        console.Output.Should().Contain(DetectedAgentUrlFormat("http://fakeurl:7777/"));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task DetectTransportHttp()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        using var agent = MockTracerAgent.Create(Output, TcpPortProvider.GetOpenPort());

        var url = $"http://127.0.0.1:{agent.Port}/";

        using var helper = await StartConsole(enableProfiler: false, ("DD_TRACE_AGENT_URL", url));

        using var console = ConsoleHelper.Redirect();

        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        _ = await AgentConnectivityCheck.RunAsync(processInfo.ExtractConfigurationSource(null, null));

        console.Output.Should().Contain(ConnectToEndpointFormat(url, "HTTP"));
    }

#if NETCOREAPP3_1_OR_GREATER
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task DetectTransportUds()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        var tracesUdsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var url = $"unix://{tracesUdsPath}";
        var uri = new System.Uri(url);

        using var agent = MockTracerAgent.Create(Output, new UnixDomainSocketConfig(tracesUdsPath, null));
        using var helper = await StartConsole(enableProfiler: false, ("DD_TRACE_AGENT_URL", url));
        using var console = ConsoleHelper.Redirect();

        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        _ = await AgentConnectivityCheck.RunAsync(processInfo.ExtractConfigurationSource(null, null));

        console.Output.Should().Contain(ConnectToEndpointFormat(uri.PathAndQuery, "domain sockets"));
    }
#endif

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NoAgent()
    {
        using var console = ConsoleHelper.Redirect();

        var result = await AgentConnectivityCheck.RunAsync(new ToolExporterSettings("http://fakeurl/"));

        result.Should().BeFalse();

        // Note for future maintainers: this assertion needs to be changed to something smarter
        // if the error message stops being at the end of the string
        console.Output.Should().Contain(ErrorDetectingAgent("http://fakeurl/", string.Empty));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task FaultyAgent()
    {
        using var console = ConsoleHelper.Redirect();

        using var agent = MockTracerAgent.Create(Output, TcpPortProvider.GetOpenPort());

        agent.CustomResponses[MockTracerResponseType.Traces] = new MockTracerResponse() { StatusCode = (int)HttpStatusCode.InternalServerError };

        var result = await AgentConnectivityCheck.RunAsync(new ToolExporterSettings($"http://localhost:{agent.Port}/"));

        result.Should().BeFalse();

        console.Output.Should().Contain(WrongStatusCodeFormat((int)HttpStatusCode.InternalServerError));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task DetectVersion()
    {
        const string expectedVersion = "7.66.55";

        using var console = ConsoleHelper.Redirect();

        using var agent = MockTracerAgent.Create(Output, TcpPortProvider.GetOpenPort());
        agent.Version = expectedVersion;

        var result = await AgentConnectivityCheck.RunAsync(new ToolExporterSettings($"http://localhost:{agent.Port}/"));

        result.Should().BeTrue();

        console.Output.Should().Contain(DetectedAgentVersionFormat(expectedVersion));
    }

#if NETCOREAPP3_1_OR_GREATER
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task DetectVersionUds()
    {
        const string expectedVersion = "7.66.55";

        using var console = ConsoleHelper.Redirect();

        var tracesUdsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        using var agent = MockTracerAgent.Create(Output, new UnixDomainSocketConfig(tracesUdsPath, null));
        agent.Version = expectedVersion;

        var settings = new ToolExporterSettings($"unix://{tracesUdsPath}");

        var result = await AgentConnectivityCheck.RunAsync(settings);

        result.Should().BeTrue();

        console.Output.Should().Contain(DetectedAgentVersionFormat(expectedVersion));
    }
#endif

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NoVersion()
    {
        using var console = ConsoleHelper.Redirect();

        using var agent = MockTracerAgent.Create(Output, TcpPortProvider.GetOpenPort());
        agent.Version = null;

        var result = await AgentConnectivityCheck.RunAsync(new ToolExporterSettings($"http://localhost:{agent.Port}/"));

        result.Should().BeTrue();

        console.Output.Should().Contain(AgentDetectionFailed);
    }
}
