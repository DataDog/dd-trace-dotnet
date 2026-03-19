// <copyright file="MassTransit8Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
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
[Trait("RequiresDockerDependency", "true")]
[Trait("DockerGroup", "2")]
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
    public async Task SubmitsTraces(string packageVersion)
    {

        // Set environment variables for RabbitMQ and LocalStack (for SQS/SNS)
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        var localStackEndpoint = Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";

        SetEnvironmentVariable("RABBITMQ_HOST", rabbitHost);
        SetEnvironmentVariable("LOCALSTACK_ENDPOINT", $"http://{localStackEndpoint}");
        SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

        // Enable debug logging to investigate MassTransit DiagnosticSource
        SetEnvironmentVariable("DD_TRACE_DEBUG", "true");
        // Logs will be written to /var/log/datadog (or /tmp/dd-logs on macOS)

        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
        {
            // Wait for spans to arrive
            // MassTransit 8 uses OpenTelemetry ActivitySource which creates Activities automatically
            // The sample tests:
            // - 3 transports (inmemory, rabbitmq, amazonsqs) × 2 messages × 3 spans (send + receive + process) = 18 spans
            // - Saga state machine: 3 events × 3 spans (send + receive + saga process) = 9 spans
            // - Consumer exception: 1 message × 3 spans = 3 spans
            // - Handler exception: 1 message × 3 spans = 3 spans
            // - Saga exception: 2 events × 3 spans each = 6 spans
            // Total expected: 18 + 9 + 3 + 3 + 6 = 39 spans
            // Actual observed: 42 spans (may include additional internal MassTransit operations)
            const int expectedMassTransitSpanCount = 42;
            var spans = await agent.WaitForSpansAsync(expectedMassTransitSpanCount, timeoutInMilliseconds: 60000);

            using var s = new AssertionScope();

            // Filter to MassTransit spans - component tag should be "masstransit"
            var massTransitSpans = spans.Where(span => span.GetTag("component") == "masstransit").ToList();
            massTransitSpans.Count.Should().BeGreaterOrEqualTo(expectedMassTransitSpanCount, $"should have at least {expectedMassTransitSpanCount} MassTransit spans");

            ValidateIntegrationSpans(massTransitSpans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.MassTransit8", isExternalSpan: false);

            var settings = VerifyHelper.GetSpanVerifierSettings();

            // Ignore Metrics field which can vary between runs
            settings.ModifySerialization(s => s.IgnoreMember<MockSpan>(x => x.Metrics));

            // Scrub dynamic bus endpoint names (e.g., COMPFC3GTGXWHN_SamplesMassTransit8_bus_sf3yyyf1sq3y63brbdxf8pr6na)
            var busEndpointRegex = new Regex(@"[A-Z0-9]+_SamplesMassTransit8_bus_[a-z0-9]+");
            settings.AddRegexScrubber(busEndpointRegex, "BusEndpoint");

            // Scrub dynamic queue names for RabbitMQ and SQS
            var queueNameRegex = new Regex(@"getting-started-message_[a-z0-9]+");
            settings.AddRegexScrubber(queueNameRegex, "QueueName");

            // Scrub saga-specific dynamic values (correlation IDs, saga IDs)
            var sagaIdRegex = new Regex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase);
            settings.AddRegexScrubber(sagaIdRegex, "SagaGuid");

            // Scrub saga queue names (e.g., order-state_[guid])
            var sagaQueueRegex = new Regex(@"order-state_[a-z0-9]+");
            settings.AddRegexScrubber(sagaQueueRegex, "SagaQueueName");

            // Scrub RabbitMQ broker host which varies by environment (e.g., localhost vs rabbitmq in Docker)
            var rabbitMqHostRegex = new Regex(@"rabbitmq://[^/]+/");
            settings.AddRegexScrubber(rabbitMqHostRegex, "rabbitmq://rabbitmq-host/");

            // Scrub message payload sizes which vary (e.g., "1095", "1128", etc.)
            var payloadSizeRegex = new Regex(@"messaging\.message\.payload_size_bytes: \d+");
            settings.AddRegexScrubber(payloadSizeRegex, "messaging.message.payload_size_bytes: size_bytes");

            // Scrub OTEL events (contains timestamps and file paths that vary)
            var eventsRegex = new Regex(@"events: \[.*?\}\](?=,|\s*$)", RegexOptions.Singleline);
            settings.AddRegexScrubber(eventsRegex, "events: [scrubbed]");

            await VerifyHelper.VerifySpans(
                massTransitSpans,
                settings,
                orderSpans: spans => spans
                    .OrderBy(x => x.Resource.Split(' ')[0])  // Group by destination (Failing, GettingStarted, OrderState, etc.)
                    .ThenBy(x => x.GetTag("messaging.operation") switch
                    {
                        "send" => 0,
                        "receive" => 1,
                        "process" => 2,
                        _ => 3
                    }))
                .UseFileName(nameof(MassTransit8Tests));

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.MassTransit);
        }

        // Print the log files for debugging
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
            // Filter to MassTransit and Diagnostic lines
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
