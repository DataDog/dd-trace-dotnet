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
    private readonly byte[] _flareFile = Enumerable.Repeat<byte>(43, 50).ToArray(); // repeat '+' 50 times

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task CanSendToAgent_Tcp()
    {
        const string caseId = "abc123";
        using var agent = MockTracerAgent.Create(output);
        var agentPath = new Uri($"http://localhost:{agent.Port}");
        var settings = new ImmutableExporterSettings(new ExporterSettings { AgentUri = agentPath });

        await RunTest(settings, caseId, agent);
    }

#if NETCOREAPP3_1_OR_GREATER
    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task CanSendToAgent_UDS()
    {
        const string caseId = "abc123";
        using var agent = MockTracerAgent.Create(output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
        var agentPath = agent.TracesUdsPath;
        var settings = new ImmutableExporterSettings(new ExporterSettings { TracesUnixDomainSocketPath = agentPath });

        await RunTest(settings, caseId, agent);
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
            const string caseId = "abc123";
            using var agent = MockTracerAgent.Create(output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", null));
            var pipeName = agent.TracesWindowsPipeName;
            var settings = new ImmutableExporterSettings(
                new ExporterSettings(
                    new NameValueConfigurationSource(new() { { "DD_TRACE_PIPE_NAME", pipeName } })));

            await RunTest(settings, caseId, agent);
        }
    }

    private async Task RunTest(ImmutableExporterSettings settings, string caseId, MockTracerAgent agent)
    {
        var api = TracerFlareApi.Create(settings);

        var result = await api.SendTracerFlare(WriteFlareToStreamFunc, caseId);

        var tracerFlares = agent.TracerFlareRequests;
        var (headers, form) = tracerFlares.Should().ContainSingle().Subject;

        result.Should().BeTrue();
        headers.Should().Contain("Content-Type", "multipart/form-data");
        form.GetParameterValue("source").Should().Be("tracer_dotnet");
        form.GetParameterValue("case_id").Should().Be(caseId);
        var file = form.Files.Should().ContainSingle().Subject;
        file.FileName.Should().Be("debug_logs.zip");
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
