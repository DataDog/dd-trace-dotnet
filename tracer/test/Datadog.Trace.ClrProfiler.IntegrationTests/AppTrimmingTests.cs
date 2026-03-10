// <copyright file="AppTrimmingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

#if NET6_0_OR_GREATER

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class AppTrimmingTests : TestHelper
{
    public AppTrimmingTests(ITestOutputHelper output)
        : base("Trimming", output)
    {
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    public async Task TrimmerTest()
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        var aspnetPort = TcpPortProvider.GetOpenPort();
        using var processResult = await RunSampleAndWaitForExit(agent, aspNetCorePort: aspnetPort, usePublishWithRID: true);
        var spans = await agent.WaitForSpansAsync(30);

        // Target app does 10 request, so it generates 30 spans (Http Request + AspNetCore + AspNetCore.Mvc)
        spans.Where(s => s.Name == "http.request").Should().HaveCount(10);
        spans.Where(s => s.Name == "aspnet_core.request").Should().HaveCount(10);
        spans.Where(s => s.Name == "aspnet_core_mvc.request").Should().HaveCount(10);
    }

    [SkippableFact]
    public async Task RedactedErrorLogsTest()
    {
        SetEnvironmentVariable("SEND_ERROR_LOG", "1");

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
        var aspnetPort = TcpPortProvider.GetOpenPort();
        using var processResult = await RunSampleAndWaitForExit(agent, aspNetCorePort: aspnetPort, usePublishWithRID: true);
        var spans = await agent.WaitForSpansAsync(30);

        // Target app does 10 request, so it generates 30 spans (Http Request + AspNetCore + AspNetCore.Mvc)
        spans.Where(s => s.Name == "http.request").Should().HaveCount(10);
        spans.Where(s => s.Name == "aspnet_core.request").Should().HaveCount(10);
        spans.Where(s => s.Name == "aspnet_core_mvc.request").Should().HaveCount(10);

        await agent.WaitForLatestTelemetryAsync(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppClosing));
        var allLogs = agent.Telemetry
                           .Cast<TelemetryData>()
                           .OrderBy(x => x.SeqId)
                           .Select(x => x.TryGetPayload<LogsPayload>(TelemetryRequestTypes.RedactedErrorLogs))
                           .Where(x => x is not null)
                           .SelectMany(x => x.Logs)
                           .ToList();

        allLogs.Should()
               .NotBeNullOrEmpty()
               .And.AllSatisfy(log => log.Tags.Should().Contain("trim:0"))
               .And.ContainSingle()
               .Which.Message.Should()
               .Be("Sending an error log using hacky reflection");
    }
}

#endif
