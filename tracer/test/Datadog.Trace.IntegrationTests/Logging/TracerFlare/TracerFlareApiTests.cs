// <copyright file="TracerFlareApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using HttpMultipartParser;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.Logging.TracerFlare;

public class TracerFlareApiTests(ITestOutputHelper output)
{
    private const string CaseId = "abc123";
    private const string Hostname = "some.host";
    private const string Email = "my.email@datadoghq.com";

    private readonly byte[] _flareFile = Enumerable.Repeat<byte>(43, 50).ToArray(); // repeat '+' 50 times

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task CanSendToAgent_Tcp()
    {
        using var agent = MockTracerAgent.Create(output);
        var agentPath = new Uri($"http://localhost:{agent.Port}");
        var settings = new ImmutableExporterSettings(new ExporterSettings { AgentUri = agentPath });

        await RunTest(settings, agent);
    }

#if NETCOREAPP3_1_OR_GREATER
    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task CanSendToAgent_UDS()
    {
        using var agent = MockTracerAgent.Create(output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
        var agentPath = agent.TracesUdsPath;
        var settings = new ImmutableExporterSettings(
            new ExporterSettings(
                new NameValueConfigurationSource(new() { { "DD_APM_RECEIVER_SOCKET", agentPath } })));

        await RunTest(settings, agent);
    }
#endif

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task CanSendToAgent_NamedPipes()
    {
        if (!EnvironmentTools.IsWindows())
        {
            throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
        }

        // named pipes is notoriously flaky
        var attemptsRemaining = 3;
        while (true)
        {
            try
            {
                attemptsRemaining--;
                await RunNamedPipesTest();
                return;
            }
            catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
            {
            }
        }

        async Task RunNamedPipesTest()
        {
            using var agent = MockTracerAgent.Create(output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", null));
            var pipeName = agent.TracesWindowsPipeName;
            var settings = new ImmutableExporterSettings(
                new ExporterSettings(
                    new NameValueConfigurationSource(new() { { "DD_TRACE_PIPE_NAME", pipeName } })));

            await RunTest(settings, agent);
        }
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task ReturnsFalseWhenSendFails()
    {
        using var agent = MockTracerAgent.Create(output);
        var agentPath = new Uri($"http://localhost:{agent.Port}");
        var settings = new ImmutableExporterSettings(new ExporterSettings { AgentUri = agentPath });

        var invalidJson = "{meep";
        agent.CustomResponses[MockTracerResponseType.TracerFlare] = new MockTracerResponse(invalidJson, 500);

        var api = TracerFlareApi.Create(settings);

        var result = await api.SendTracerFlare(WriteFlareToStreamFunc, CaseId, Hostname, Email);

        // It was sent, but we returned an error
        agent.TracerFlareRequests.Should().ContainSingle();
        result.Key.Should().BeFalse();
        result.Value.Should().BeNull();
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task ReturnsErrorMessageWhenSendFails()
    {
        using var agent = MockTracerAgent.Create(output);
        var agentPath = new Uri($"http://localhost:{agent.Port}");
        var settings = new ImmutableExporterSettings(new ExporterSettings { AgentUri = agentPath });

        var somethingWentWrong = "Something went wrong";
        agent.CustomResponses[MockTracerResponseType.TracerFlare] = new MockTracerResponse($$"""{ "error": "{{somethingWentWrong}}" }""", 500);

        var api = TracerFlareApi.Create(settings);

        var result = await api.SendTracerFlare(WriteFlareToStreamFunc, CaseId, Hostname, Email);

        // It was sent, but we returned an error
        agent.TracerFlareRequests.Should().ContainSingle();
        result.Key.Should().BeFalse();
        result.Value.Should().Be(somethingWentWrong);
    }

    private async Task RunTest(ImmutableExporterSettings settings, MockTracerAgent agent)
    {
        var api = TracerFlareApi.Create(settings);

        var result = await api.SendTracerFlare(WriteFlareToStreamFunc, CaseId, Hostname, Email);

        var tracerFlares = agent.TracerFlareRequests;
        var (headers, form) = tracerFlares.Should().ContainSingle().Subject;

        result.Key.Should().BeTrue();
        result.Value.Should().BeNull();
        headers.Should()
               .ContainKey("Content-Type")
               .WhoseValue
               .Split(';')
               .Should()
               .Contain("multipart/form-data")
               .And.ContainSingle(x => x.Trim().StartsWith("boundary="));
        form.GetParameterValue("source").Should().Be("tracer_dotnet");
        form.GetParameterValue("case_id").Should().Be(CaseId);
        form.GetParameterValue("hostname").Should().Be(Hostname);
        form.GetParameterValue("email").Should().Be(Email);
        var file = form.Files.Should().ContainSingle().Subject;
        file.FileName.Should().StartWith($"tracer-dotnet-{CaseId}-").And.EndWith("-debug.zip");
        file.Name.Should().Be("flare_file");
        file.ContentType.Should().Be("application/octet-stream");
        file.Data.Length.Should().Be(_flareFile.Length);
        // read the data
        using var ms = new MemoryStream(_flareFile.Length);
        await file.Data.CopyToAsync(ms);
        ms.GetBuffer().Should().Equal(_flareFile);
    }

    private Task WriteFlareToStreamFunc(Stream stream)
    {
        return stream.WriteAsync(_flareFile, offset: 0, count: _flareFile.Length);
    }
}
