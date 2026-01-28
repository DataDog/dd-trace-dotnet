// <copyright file="MassTransit7Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
public class MassTransit7Tests : TracingIntegrationTest
{
    public MassTransit7Tests(ITestOutputHelper output)
        : base("MassTransit7", output)
    {
        SetServiceVersion("1.0.0");
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
        span.IsMassTransit(metadataSchemaVersion);

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces()
    {
        // Set environment variables for RabbitMQ and LocalStack (for SQS/SNS)
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        var localStackEndpoint = Environment.GetEnvironmentVariable("AWS_SDK_HOST") ?? "localhost:4566";

        SetEnvironmentVariable("RABBITMQ_HOST", rabbitHost);
        SetEnvironmentVariable("LOCALSTACK_ENDPOINT", $"http://{localStackEndpoint}");

        // Enable debug logging to investigate MassTransit DiagnosticSource
        SetEnvironmentVariable("DD_TRACE_DEBUG", "true");
        var logDir = Path.Combine(LogDirectory, nameof(SubmitsTraces));
        Directory.CreateDirectory(logDir);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);

        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent))
        {
            // Wait for spans to arrive
            // The sample tests 3 transports (in-memory, RabbitMQ, Amazon SQS) with 2 messages each
            // Plus a saga state machine with 3 events
            // Each message produces: 1 send span + 1 receive span + 1 process span = 3 spans
            // But DiagnosticSource only emits send and receive/consume events, so:
            // - 3 transports × 2 messages × 2 spans (send + receive) = 12 spans
            // - Saga: 3 events × 2 spans (send + receive) = 6 spans
            // Total expected: ~24 MassTransit spans (may vary based on transport behavior)
            const int expectedMassTransitSpanCount = 24;
            var spans = await agent.WaitForSpansAsync(expectedMassTransitSpanCount, timeoutInMilliseconds: 60000);

            using var s = new AssertionScope();

            // Filter to MassTransit spans - component tag should be "MassTransit"
            var massTransitSpans = spans.Where(span => span.GetTag("component") == "MassTransit").ToList();
            massTransitSpans.Count.Should().BeGreaterOrEqualTo(expectedMassTransitSpanCount, $"should have at least {expectedMassTransitSpanCount} MassTransit spans");

            ValidateIntegrationSpans(massTransitSpans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.MassTransit7", isExternalSpan: false);

            var settings = VerifyHelper.GetSpanVerifierSettings();

            // Scrub dynamic bus endpoint names (e.g., COMPFC3GTGXWHN_SamplesMassTransit7_bus_sf3yyyf1sq3y63brbdxf8pr6na)
            var busEndpointRegex = new Regex(@"[A-Z0-9]+_SamplesMassTransit7_bus_[a-z0-9]+");
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

            await VerifyHelper.VerifySpans(
                massTransitSpans,
                settings,
                orderSpans: spans => spans
                    .OrderBy(x => x.GetTag("messaging.system"))
                    .ThenBy(x => x.GetTag("messaging.operation"))
                    .ThenBy(x => x.Start)
                    .ThenBy(x => x.Name))
                .UseFileName(nameof(MassTransit7Tests));

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.MassTransit);
        }

        // Print the log files for debugging
        PrintMassTransitLogs(logDir);
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
