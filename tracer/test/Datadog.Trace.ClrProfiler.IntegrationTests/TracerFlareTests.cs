// <copyright file="TracerFlareTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO.Compression;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using HttpMultipartParser;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[Collection(nameof(DynamicConfigurationTests))]
public class TracerFlareTests : TestHelper
{
    private const string CaseId = "abc123";

    private const string LogFileNamePrefix = "dotnet-tracer-managed-";
    private const string DiagnosticLog = "DATADOG TRACER CONFIGURATION";
    private const string Email = "test_user";
    private const string Hostname = "integration_tests";

    public TracerFlareTests(ITestOutputHelper output)
        : base("Console", output)
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "5");
    }

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("RunOnWindows", "True")]
    public async Task SendTracerFlare(bool logLevelInFileName)
    {
        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
        var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Console";
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*", LogDirectory);
        using var sample = await StartSample(agent, "wait", string.Empty, aspNetCorePort: 5000);

        try
        {
            _ = await logEntryWatcher.WaitForLogEntry(DiagnosticLog);

            await InitializeFlare(agent, logEntryWatcher, logLevelInFileName);
            await TriggerFlareCollection(agent, logEntryWatcher);
        }
        finally
        {
            if (!sample.HasExited)
            {
                sample.Kill();
            }
        }

        var (_, flare) = agent.TracerFlareRequests.Should().ContainSingle().Subject;
        flare.GetParameterValue("case_id").Should().Be(CaseId);
        flare.GetParameterValue("email").Should().Be(Email);
        flare.GetParameterValue("hostname").Should().Be(Hostname);
        var flareFile = flare.Files.Should().ContainSingle().Subject;
        flareFile.Name.Should().Be("flare_file");
        flareFile.ContentType.Should().Be("application/octet-stream");

        var zip = new ZipArchive(flareFile.Data, ZipArchiveMode.Read);
        zip.Entries.Should().NotBeNullOrEmpty();
        zip.Entries
           .Should()
           .Contain(x => x.Name.StartsWith("dotnet-tracer-managed-"))
           .And.Contain(x => x.Name.StartsWith("dotnet-native-loader-"))
           .And.Contain(x => x.Name.StartsWith("dotnet-tracer-native-"))
           .And.Contain(x => x.Name.StartsWith("dotnet-tracer-telemetry-"));
    }

    private async Task InitializeFlare(MockTracerAgent agent, LogEntryWatcher logEntryWatcher, bool logLevelInFileName)
    {
        string fileId;
        object payload;

        if (logLevelInFileName)
        {
            fileId = "flare-log-level.debug";
            payload = new { };
        }
        else
        {
            fileId = Guid.NewGuid().ToString();
            payload = new { config = new { log_level = "debug" } };
        }

        var request = await agent.SetupRcmAndWait(Output, new[] { (payload, RcmProducts.TracerFlareInitiated, fileId) });

        request.Should().NotBeNull();
        request.Client.State.ConfigStates.Should().ContainSingle(f => f.Id == fileId)
               .Subject.ApplyState.Should().Be(ApplyStates.ACKNOWLEDGED);

        var logs = await logEntryWatcher.WaitForLogEntries(new[] { TracerFlareManager.TracerFlareInitializationLog });
        logs.Should().Contain(x => x.Contains(TracerFlareManager.TracerFlareInitializationLog));
    }

    private async Task TriggerFlareCollection(MockTracerAgent agent, LogEntryWatcher logEntryWatcher)
    {
        object config = new
        {
            task_type = "tracer_flare",
            args = new
            {
                case_id = CaseId,
                user_handle = Email,
                hostname = Hostname,
            }
        };
        var fileId = Guid.NewGuid().ToString();

        // This will also remove the AGENT_CONF log at the same time
        var request = await agent.SetupRcmAndWait(Output, new[] { (config, RcmProducts.TracerFlareRequested, fileId) });

        request.Client.State.ConfigStates.Should()
               .OnlyContain(f => f.Id == fileId)
               .And.OnlyContain(x => x.ApplyState == ApplyStates.ACKNOWLEDGED);

        var logs = await logEntryWatcher.WaitForLogEntries(new[] { TracerFlareApi.TracerFlareSentLog, TracerFlareManager.TracerFlareCompleteLog });
        logs.Should().Contain(x => x.Contains(TracerFlareManager.TracerFlareCompleteLog));
        logs.Should().Contain(x => x.Contains(TracerFlareApi.TracerFlareSentLog));
    }
}
