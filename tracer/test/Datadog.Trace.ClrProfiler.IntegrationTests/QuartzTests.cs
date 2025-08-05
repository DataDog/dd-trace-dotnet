// <copyright file="QuartzTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Serilog;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
public class QuartzTests : TracingIntegrationTest
{
    private static readonly HashSet<string> ExcludeTags = ["fire.instance.id"];
    private readonly Regex _versionRegex = new(@"telemetry.sdk.version: (0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)");
    private readonly Regex _timeUnixNanoRegex = new(@"time_unix_nano"":([0-9]{10}[0-9]+)");

    public QuartzTests(ITestOutputHelper output)
        : base("Quartz", output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> GetData() => PackageVersions.Quartz;

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsQuartz(metadataSchemaVersion, ExcludeTags);

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [MemberData(nameof(PackageVersions.OpenTelemetry), MemberType = typeof(PackageVersions))]
    public async Task SubmitsTraces(string packageVersion)
    {
        SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            Log.Information("packageversion: " + packageVersion);
            Log.Information("version from get suffix: " + GetSuffix(packageVersion));
            var filename = nameof(QuartzTests) + GetSuffix(packageVersion);
            Log.Information(filename);
            const int expectedSpanCount = 2;
            var spans = await agent.WaitForSpansAsync(expectedSpanCount);

            using var s = new AssertionScope();
            spans.Count.Should().Be(expectedSpanCount);

            var myServiceNameSpans = spans.Where(s => s.Service == "samples.quartz.consoleapp");

            ValidateIntegrationSpans(myServiceNameSpans, metadataSchemaVersion: "v0", expectedServiceName: "samples.quartz.consoleapp", isExternalSpan: false);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            var traceStatePRegex = new Regex("p:[0-9a-fA-F]+");
            var traceIdRegexHigh = new Regex("TraceIdLow: [0-9]+");
            var traceIdRegexLow = new Regex("TraceIdHigh: [0-9]+");
            var fireInstanceId = new Regex(@"fire\.instance\.id:\s*\d+");
            settings.AddRegexScrubber(traceStatePRegex, "p:TsParentId");
            settings.AddRegexScrubber(traceIdRegexHigh, "TraceIdHigh: LinkIdHigh");
            settings.AddRegexScrubber(traceIdRegexLow, "TraceIdLow: LinkIdLow");
            settings.AddRegexScrubber(_timeUnixNanoRegex, @"time_unix_nano"":<DateTimeOffset.Now>");
            settings.AddRegexScrubber(_versionRegex, "telemetry.sdk.version: sdk-version");
            settings.AddRegexScrubber(fireInstanceId, "fire.instance.id: <fire.instance.id>");

            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename);

            // this isn't real
            // await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Quartz);
        }
    }

    private static string GetSuffix(string packageVersion)
    {
        if (!string.IsNullOrEmpty(packageVersion)
         && new Version(packageVersion) >= new Version("3.1.0") && new Version(packageVersion) < new Version("4.0.0"))
        {
            return "V3";
        }

        if (!string.IsNullOrEmpty(packageVersion)
         && new Version(packageVersion) >= new Version("4.0.0"))
        {
            return "V4";
        }

        return "no-package-version";
    }
}
