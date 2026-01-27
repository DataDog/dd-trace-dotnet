// <copyright file="MassTransit7Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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

        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent))
        {
            // Wait for spans to arrive - the sample tests 3 transports + saga state machine:
            // Each transport produces 2 MassTransit spans (receive + process) = 6 spans
            // Saga test produces 6 MassTransit spans (3 events × 2 spans each) = 6 spans
            // Total: 12 MassTransit spans
            // Note: We wait for at least 12 spans, but more will arrive from RabbitMQ/SQS integrations
            const int expectedMassTransitSpanCount = 12;
            var spans = await agent.WaitForSpansAsync(expectedMassTransitSpanCount, timeoutInMilliseconds: 60000);

            using var s = new AssertionScope();

            // Filter to MassTransit spans - component tag should be "MassTransit"
            var massTransitSpans = spans.Where(span => span.GetTag("component") == "MassTransit").ToList();
            massTransitSpans.Count.Should().Be(expectedMassTransitSpanCount, "should have exactly 12 MassTransit spans (2 per transport × 3 transports + 2 per saga event × 3 events)");

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
    }
}
