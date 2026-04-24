// <copyright file="MassTransit8Tests.cs" company="Datadog">
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
public class MassTransit8Tests : TracingIntegrationTest
{
    public MassTransit8Tests(ITestOutputHelper output)
        : base("MassTransit8", output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> GetData() => PackageVersions.MassTransit8;

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
        const int expectedMassTransitSpanCount = 27;
        var prefix = IsWindows() ? "InMemoryWindows" : "InMemory";
        await RunTransportTest(packageVersion, expectedMassTransitSpanCount, prefix);
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

        // Ignore Metrics field which can vary between runs
        settings.ModifySerialization(ss => ss.IgnoreMember<MockSpan>(x => x.Metrics));

        // Scrub dynamic bus endpoint names (e.g., COMPFC3GTGXWHN_SamplesMassTransit8_bus_sf3yyyf1sq3y63brbdxf8pr6na)
        settings.AddRegexScrubber(new Regex(@"[A-Za-z0-9]+_SamplesMassTransit8_bus_[a-z0-9]+"), "BusEndpoint");

        // Scrub dynamic per-transport queue names (per-message-type suffix with random hash)
        settings.AddRegexScrubber(new Regex(@"getting-started-with-(?:in-memory|rabbit-mq|sqs)_[a-z0-9]+"), "QueueName");

        // Scrub saga-specific dynamic values (correlation IDs, saga IDs)
        settings.AddRegexScrubber(new Regex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase), "SagaGuid");

        // Scrub saga queue names (e.g., order-state_[guid])
        settings.AddRegexScrubber(new Regex(@"order-state_[a-z0-9]+"), "SagaQueueName");

        // Scrub RabbitMQ broker host which varies by environment (e.g., localhost vs rabbitmq in Docker)
        settings.AddRegexScrubber(new Regex(@"rabbitmq://[^/]+/"), "rabbitmq://rabbitmq-host/");

        // Scrub message payload sizes (vary between runs). Older MT8 uses underscores, newer uses dots.
        settings.AddRegexScrubber(new Regex(@"messaging\.message[._]payload_size_bytes: \d+"), "messaging.message.payload_size_bytes: size_bytes");

        // Network/container address tags vary between local Docker runs and CI agents.
        settings.AddRegexScrubber(new Regex(@"client\.address: [^,\r\n]+"), "client.address: client-address");
        settings.AddRegexScrubber(new Regex(@"server\.address: [^,\r\n]+"), "server.address: server-address");
        settings.AddRegexScrubber(new Regex(@"network\.local\.address: [^,\r\n]+"), "network.local.address: local-address");
        settings.AddRegexScrubber(new Regex(@"network\.peer\.address: [^,\r\n]+"), "network.peer.address: peer-address");
        settings.AddRegexScrubber(new Regex(@"network\.type: [^,\r\n]+"), "network.type: network-type");

        // Scrub MassTransit OTEL library version, which varies with the tested package version
        settings.AddRegexScrubber(new Regex(@"otel\.library\.version: [\d\.]+"), "otel.library.version: masstransit-version");

        // Remove optional messaging.message.body.size tag (only present in some MassTransit versions)
        settings.AddRegexScrubber(new Regex(@"messaging\.message\.body\.size: \d+"), "messaging.message.body.size: body_size");

        // Scrub OTEL events (contains timestamps and file paths that vary)
        settings.AddRegexScrubber(new Regex(@"events: \[.*?\}\](?=,|\s*$)", RegexOptions.Singleline), "events: [scrubbed]");

        // Keep only the first line of error.stack (exception type + message) and drop
        // stack frames, which vary across .NET runtimes (e.g., the
        // "--- End of stack trace from previous location ---" async rethrow marker
        // appears on some runtimes but not others).
        settings.AddRegexScrubber(new Regex(@"error\.stack:[^\n]*\n([^\n]+)\n(?:[^\n]*\n)*?(?=\s{6}\w)", RegexOptions.Multiline), "error.stack: $1\n");

        return settings;
    }

    private static string GetSuffix(string packageVersion)
    {
        // Default csproj version is 8.5.8 which falls in the 8.3.2+ (base) tier.
        if (string.IsNullOrEmpty(packageVersion))
        {
            return string.Empty;
        }

        return new Version(packageVersion) switch
        {
            { } v when v <= new Version("8.0.5") => ".pre_8_0_5",
            { } v when v <= new Version("8.0.6") => ".pre_8_0_6",
            { } v when v <= new Version("8.0.7") => ".pre_8_0_7",
            { } v when v <= new Version("8.0.9") => ".pre_8_0_9",
            { } v when v <= new Version("8.0.14") => ".pre_8_0_14",
            { } v when v <= new Version("8.0.16") => ".pre_8_0_16",
            { } v when v <= new Version("8.1.0") => ".pre_8_1",
            { } v when v <= new Version("8.2.0") => ".pre_8_2",
            { } v when v <= new Version("8.3.1") => ".8_2_1_to_8_3_1",
            _ => string.Empty, // 8.3.2+ = base snapshot
        };
    }

    private void SetCommonEnvironmentVariables()
    {
        SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");
        SetEnvironmentVariable("DD_SERVICE", "Samples.MassTransit8");
        SetEnvironmentVariable("DD_TRACE_DEBUG", "true");
    }

    private async Task RunTransportTest(
        string packageVersion,
        int expectedMassTransitSpanCount,
        string variantPrefix)
    {
        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            var spans = await agent.WaitForSpansAsync(expectedMassTransitSpanCount, timeoutInMilliseconds: 60000);

            using var s = new AssertionScope();

            var massTransitSpans = spans.Where(span => span.GetTag("component") == "masstransit").ToList();
            massTransitSpans.Count.Should().BeGreaterOrEqualTo(expectedMassTransitSpanCount, $"should have at least {expectedMassTransitSpanCount} MassTransit spans");

            ValidateIntegrationSpans(massTransitSpans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.MassTransit8", isExternalSpan: false);

            var settings = BuildSpanVerifierSettings();

            var fileName = nameof(MassTransit8Tests) + variantPrefix + GetSuffix(packageVersion);

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
                .UseFileName(fileName)
                .UseDirectory("MassTransit8");

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
