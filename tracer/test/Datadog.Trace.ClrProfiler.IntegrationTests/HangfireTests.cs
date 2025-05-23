// <copyright file="HangfireTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests;
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

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces()
    {
        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent))
        {
            const int expectedSpanCount = 53;
            var spans = agent.WaitForSpans(expectedSpanCount);

            using var s = new AssertionScope();
            spans.Count.Should().Be(expectedSpanCount);

            var myServiceNameSpans = spans.Where(s => s.Service == "MyServiceName");

            ValidateIntegrationSpans(myServiceNameSpans, metadataSchemaVersion: "v0", expectedServiceName: "MyServiceName", isExternalSpan: false);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            var traceStatePRegex = new Regex("p:[0-9a-fA-F]+");
            var traceIdRegexHigh = new Regex("TraceIdLow: [0-9]+");
            var traceIdRegexLow = new Regex("TraceIdHigh: [0-9]+");
            settings.AddRegexScrubber(traceStatePRegex, "p:TsParentId");
            settings.AddRegexScrubber(traceIdRegexHigh, "TraceIdHigh: LinkIdHigh");
            settings.AddRegexScrubber(traceIdRegexLow, "TraceIdLow: LinkIdLow");
            settings.AddRegexScrubber(_timeUnixNanoRegex, @"time_unix_nano"":<DateTimeOffset.Now>");
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(nameof(HangfireTests));

            telemetry.AssertIntegrationEnabled(IntegrationId.Hangfire);
        }
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsHangfire(metadataSchemaVersion);
}
