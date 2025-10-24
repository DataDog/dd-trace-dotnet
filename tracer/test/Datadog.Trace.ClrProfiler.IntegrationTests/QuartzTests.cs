// <copyright file="QuartzTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
public class QuartzTests : TracingIntegrationTest
{
    private readonly Regex _versionRegex = new(@"telemetry.sdk.version: (0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)");
    private readonly Regex _timeUnixNanoRegex = new(@"time_unix_nano"":([0-9]{10}[0-9]+)");

    public QuartzTests(ITestOutputHelper output)
        : base("Quartz", output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> GetData()
    {
        var data = PackageVersions.Quartz;
        if (data == null || !data.Any())
        {
            // Fallback if PackageVersions.Quartz returns null or empty
            return new[] { new object[] { string.Empty } };
        }

        return data;
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsQuartz(metadataSchemaVersion);

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(GetData))]
    public async Task SubmitsTraces(string packageVersion)
    {
        SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            var (suffix, expectedSpanCount) = GetSuffix(packageVersion);
            var filename = nameof(QuartzTests) + suffix;
            var spans = await agent.WaitForSpansAsync(expectedSpanCount);

            using var s = new AssertionScope();
            spans.Count.Should().Be(expectedSpanCount);

            var myServiceNameSpans = spans.Where(s => s.Service == "Samples.Quartz");
            ValidateIntegrationSpans(myServiceNameSpans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.Quartz", isExternalSpan: false);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            var traceStatePRegex = new Regex("p:[0-9a-fA-F]+");
            var traceIdRegexHigh = new Regex("TraceIdLow: [0-9]+");
            var traceIdRegexLow = new Regex("TraceIdHigh: [0-9]+");
            var fireInstanceId = new Regex(@"fire\.instance\.id:\s*\d+");
            var scrubEvents = new Regex(@"events:?\s*:\s*\[(?s:.*?)\],");
            var scrubOtelVersion = new Regex(@"otel\.library\.version:\s*[\d\.]+");
            settings.AddRegexScrubber(traceStatePRegex, "p:TsParentId");
            settings.AddRegexScrubber(traceIdRegexHigh, "TraceIdHigh: LinkIdHigh");
            settings.AddRegexScrubber(traceIdRegexLow, "TraceIdLow: LinkIdLow");
            settings.AddRegexScrubber(_timeUnixNanoRegex, @"time_unix_nano"":<DateTimeOffset.Now>");
            settings.AddRegexScrubber(_versionRegex, "telemetry.sdk.version: sdk-version");
            settings.AddRegexScrubber(fireInstanceId, "fire.instance.id: <fire.instance.id>");
            settings.AddRegexScrubber(scrubEvents, "events: <events>,");
            settings.AddRegexScrubber(scrubOtelVersion, "otel.library.version: <otel-library-version>");

            await VerifyHelper.VerifySpans(
                                        spans,
                                        settings,
                                        orderSpans: s => s
                                                        .OrderBy(x => x.Name)
                                                        .ThenBy(x => x.Resource)
                                                        .ThenBy(x => x.Error))
                              .UseFileName(filename);

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.OpenTelemetry);
        }
    }

    private static Tuple<string, int> GetSuffix(string packageVersion)
    {
        if (string.IsNullOrEmpty(packageVersion))
        {
#if NETCOREAPP3_0 || NETCOREAPP3_1
            return new("V3NETCOREAPP3X", 2);
#elif NETFRAMEWORK
            return new("V3NETFRAMEWORK", 2);
#else
            return new("V3", 2);
#endif
        }

        return new Version(packageVersion) switch
        {
            { } v when v >= new Version("4.0.0") => new("V4", 3),
#if NETCOREAPP3_0 || NETCOREAPP3_1
            { } v when v >= new Version("3.0.0") => new("V3NETCOREAPP3X", 2),
#elif  NETFRAMEWORK
            { } v when v >= new Version("3.0.0") => new("V3NETFRAMEWORK", 2),
#endif
            _ => new("V3", 2)
        };
    }
}
