// <copyright file="HangfireTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
public class HangfireTests : TracingIntegrationTest
{
    private readonly Regex _timeUnixNanoRegex = new(@"time_unix_nano"":([0-9]{10}[0-9]+)");

    public HangfireTests(ITestOutputHelper output)
        : base("Hangfire", output)
    {
        SetServiceVersion("1.0.0");
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsHangfire(metadataSchemaVersion);

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task BaggageDoesNotAccumulateAcrossSequentialJobs()
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent, arguments: "baggage");

        var workerThreadIds = Regex.Matches(process.StandardOutput, @"Worker thread for job-(?:one|two): (\d+)")
                                   .Cast<Match>()
                                   .Select(x => x.Groups[1].Value)
                                   .ToArray();

        workerThreadIds.Should().HaveCount(2);
        workerThreadIds.Distinct().Should().ContainSingle();
        process.StandardOutput.Should().Contain("Baggage for job-one: [job-one=one]");
        process.StandardOutput.Should().Contain("Baggage for job-two: [job-two=two]");
    }

    [SkippableTheory]
    [InlineData("custom-filter-collection")]
    [InlineData("custom-filter-provider")]
    [InlineData("custom-filter-combined")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTracesWithApplicationJobFilterCollections(string scenario)
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent, arguments: scenario);

        var jobs = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 15000, operationName: "hangfire.perform");
        var parents = await agent.WaitForSpansAsync(1, timeoutInMilliseconds: 15000, operationName: "internal");
        jobs.Should().HaveCount(1);
        parents.Should().HaveCount(1);
        jobs[0].Error.Should().Be(0);
        jobs[0].TraceId.Should().Be(parents[0].TraceId);
        jobs[0].ParentId.Should().Be(parents[0].SpanId);
        if (scenario == "custom-filter-combined")
        {
            // The span must cover PostPerformDelayFilter, which runs between the two Datadog filters.
            jobs[0].Duration.Should().BeGreaterThanOrEqualTo(300_000_000); // ns
        }
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces()
    {
        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent))
        {
            // Not testing for retry attempts
            const int expectedSpanCount = 3;
            var spans = await agent.WaitForSpansAsync(expectedSpanCount, timeoutInMilliseconds: 15000);
            using var s = new AssertionScope();
            spans.Count.Should().Be(expectedSpanCount);

            var myServiceNameSpans = spans.Where(s => s.GetTag("component") == "hangfire");

            ValidateIntegrationSpans(myServiceNameSpans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.Hangfire", isExternalSpan: false);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            var traceStatePRegex = new Regex("p:[0-9a-fA-F]+");
            var traceIdRegexHigh = new Regex("TraceIdLow: [0-9]+");
            var traceIdRegexLow = new Regex("TraceIdHigh: [0-9]+");
            var hangfireInnerExceptionJoin = new Regex(@"\.\s*\r?\n\s*--->");
            settings.AddRegexScrubber(traceStatePRegex, "p:TsParentId");
            settings.AddRegexScrubber(traceIdRegexHigh, "TraceIdHigh: LinkIdHigh");
            settings.AddRegexScrubber(traceIdRegexLow, "TraceIdLow: LinkIdLow");
            settings.AddRegexScrubber(_timeUnixNanoRegex, @"time_unix_nano"":<DateTimeOffset.Now>");
            settings.AddRegexScrubber(hangfireInnerExceptionJoin, ". --->");
            await VerifyHelper.VerifySpans(
                                            spans,
                                            settings,
                                            orderSpans: s => s
                                                                                        .OrderBy(x => x.Name)
                                                                                        .ThenBy(x => x.Resource)
                                                                                        .ThenBy(x => x.Error))
                                            .UseFileName(nameof(HangfireTests));
            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Hangfire);
        }
    }
}
