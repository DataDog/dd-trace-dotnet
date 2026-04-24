// <copyright file="MassTransit7Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
public class MassTransit7Tests : TracingIntegrationTest
{
    public MassTransit7Tests(ITestOutputHelper output)
        : base("MassTransit7", output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> GetData() => PackageVersions.MassTransit7;

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
        span.IsMassTransit(metadataSchemaVersion);

    [SkippableTheory]
    [MemberData(nameof(GetData))]
    [Trait("Category", "EndToEnd")]
    [Trait("DockerGroup", "1")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces_InMemory(string packageVersion)
    {
        SetEnvironmentVariable("MASSTRANSIT_TRANSPORT", "inmemory");
        SetCommonEnvironmentVariables();

        // InMemory transport + saga + 3 exception scenarios
        const int expectedMassTransitSpanCount = 30;
        var snapshotSuffix = IsWindows() ? "InMemoryWindows" : "InMemory";
        await RunTransportTest(packageVersion, expectedMassTransitSpanCount, snapshotSuffix);
    }

    [SkippableTheory]
    [MemberData(nameof(GetData))]
    [Trait("Category", "EndToEnd")]
    [Trait("DockerGroup", "1")]
    [Trait("RequiresDockerDependency", "true")]
    public async Task SubmitsTraces_RabbitMq(string packageVersion)
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        SetEnvironmentVariable("RABBITMQ_HOST", rabbitHost);
        SetEnvironmentVariable("MASSTRANSIT_TRANSPORT", "rabbitmq");
        SetCommonEnvironmentVariables();

        // RabbitMq only: 1 transport × 2 messages × 3 spans = 6
        const int expectedMassTransitSpanCount = 6;
        await RunTransportTest(packageVersion, expectedMassTransitSpanCount, "RabbitMq");
    }

    [SkippableTheory]
    [MemberData(nameof(GetData))]
    [Trait("Category", "EndToEnd")]
    [Trait("DockerGroup", "2")]
    [Trait("RequiresDockerDependency", "true")]
    public async Task SubmitsTraces_Sqs(string packageVersion)
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        var localStackEndpoint = Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";
        SetEnvironmentVariable("LOCALSTACK_ENDPOINT", $"http://{localStackEndpoint}");
        SetEnvironmentVariable("MASSTRANSIT_TRANSPORT", "amazonsqs");
        SetCommonEnvironmentVariables();

        // Sqs only: 1 transport × 2 messages × 3 spans = 6
        const int expectedMassTransitSpanCount = 6;
        await RunTransportTest(packageVersion, expectedMassTransitSpanCount, "Sqs");
    }

    private static bool IsWindows() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static VerifySettings BuildSpanVerifierSettings()
    {
        var settings = VerifyHelper.GetSpanVerifierSettings();

        // Scrub dynamic bus endpoint names (e.g., COMPFC3GTGXWHN_SamplesMassTransit7_bus_sf3yyyf1sq3y63brbdxf8pr6na)
        settings.AddRegexScrubber(new Regex(@"[A-Za-z0-9]+_SamplesMassTransit7_bus_[a-z0-9]+"), "BusEndpoint");

        // Scrub dynamic per-transport queue names (per-message-type suffix with random hash)
        settings.AddRegexScrubber(new Regex(@"getting-started-with-(?:in-memory|rabbit-mq|sqs)_[a-z0-9]+"), "QueueName");

        // Scrub saga-specific dynamic values (correlation IDs, saga IDs)
        settings.AddRegexScrubber(new Regex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase), "SagaGuid");

        // Scrub saga queue names (e.g., order-state_[guid])
        settings.AddRegexScrubber(new Regex(@"order-state_[a-z0-9]+"), "SagaQueueName");

        // Scrub RabbitMQ broker host which varies by environment (e.g., localhost vs rabbitmq_arm64)
        settings.AddRegexScrubber(new Regex(@"rabbitmq://[^/]+/"), "rabbitmq://rabbitmq-host/");

        // Remove optional messaging.message.body.size tag (only present in some MassTransit versions)
        settings.AddRegexScrubber(new Regex(@"messaging\.message\.body\.size: \d+"), "messaging.message.body.size: body_size");

        // Keep only the first line of error.stack (exception type + message) and drop
        // stack frames, which vary across .NET runtimes (e.g., the
        // "--- End of stack trace from previous location ---" async rethrow marker
        // appears on some runtimes but not others).
        settings.AddRegexScrubber(new Regex(@"error\.stack:[^\n]*\n([^\n]+)\n(?:[^\n]*\n)*?(?=\s{6}\w)", RegexOptions.Multiline), "error.stack: $1\n");

        return settings;
    }

    private void SetCommonEnvironmentVariables()
    {
        SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "false");
        SetEnvironmentVariable("DD_TRACE_DEBUG", "true");
    }

    private async Task RunTransportTest(
        string packageVersion,
        int expectedMassTransitSpanCount,
        string snapshotSuffix)
    {
        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            var spans = await agent.WaitForSpansAsync(expectedMassTransitSpanCount, timeoutInMilliseconds: 60000);

            using var s = new AssertionScope();

            var massTransitSpans = spans.Where(span => span.GetTag("component") == "masstransit").ToList();
            massTransitSpans.Count.Should().BeGreaterOrEqualTo(expectedMassTransitSpanCount, $"should have at least {expectedMassTransitSpanCount} MassTransit spans");

            ValidateIntegrationSpans(massTransitSpans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.MassTransit7", isExternalSpan: false);

            var settings = BuildSpanVerifierSettings();

            await VerifyHelper.VerifySpans(
                massTransitSpans,
                settings,
                orderSpans: spans => spans
                    .OrderBy(x => x.Start)
                    .ThenBy(x => x.GetTag("messaging.operation") switch
                    {
                        "send" => 0,
                        "receive" => 1,
                        "process" => 2,
                        _ => 3
                    }))
                .UseFileName(nameof(MassTransit7Tests) + snapshotSuffix);

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.MassTransit);
        }

        PrintMassTransitLogs("/tmp/dd-logs");
    }

    private void PrintMassTransitLogs(string logDir)
    {
        Output.WriteLine($"Log directory: {logDir}");
        if (!Directory.Exists(logDir))
        {
            Output.WriteLine("Log directory does not exist");
            return;
        }

        foreach (var logFile in Directory.GetFiles(logDir, "*.log"))
        {
            Output.WriteLine($"=== {Path.GetFileName(logFile)} ===");
            var content = File.ReadAllText(logFile);
            foreach (var line in content.Split('\n'))
            {
                if (line.Contains("MassTransit") || line.Contains("Diagnostic"))
                {
                    Output.WriteLine(line);
                }
            }
        }
    }
}
