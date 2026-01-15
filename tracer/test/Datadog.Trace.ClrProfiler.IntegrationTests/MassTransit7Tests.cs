// <copyright file="MassTransit7Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
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
        using (var telemetry = this.ConfigureTelemetry())
        using (var agent = EnvironmentHelper.GetMockAgent())
        using (await RunSampleAndWaitForExit(agent))
        {
            // Wait for spans to arrive - expecting send, receive, and process spans
            const int minExpectedSpanCount = 12;
            var spans = await agent.WaitForSpansAsync(minExpectedSpanCount, timeoutInMilliseconds: 30000);

            using var s = new AssertionScope();
            spans.Count.Should().BeGreaterOrEqualTo(minExpectedSpanCount);

            // Filter to MassTransit spans - component tag should be "MassTransit"
            var massTransitSpans = spans.Where(span => span.GetTag("component") == "MassTransit").ToList();
            massTransitSpans.Should().NotBeEmpty("should have MassTransit spans with component tag");

            // Output all spans for debugging
            Output.WriteLine($"Total spans: {spans.Count}, MassTransit spans: {massTransitSpans.Count}");
            foreach (var span in massTransitSpans)
            {
                Output.WriteLine($"Span: Name={span.Name}, Resource={span.Resource}, TraceId={span.TraceId}, SpanId={span.SpanId}, ParentId={span.ParentId}, Operation={span.GetTag("messaging.operation")}, Type={span.Type}");
            }

            // Verify we have receive and process operations
            var receiveSpans = massTransitSpans.Where(span => span.GetTag("messaging.operation") == "receive").ToList();
            var processSpans = massTransitSpans.Where(span => span.GetTag("messaging.operation") == "process").ToList();
            var sendSpans = massTransitSpans.Where(span => span.GetTag("messaging.operation") == "send").ToList();

            receiveSpans.Should().NotBeEmpty("should have receive spans");
            processSpans.Should().NotBeEmpty("should have process spans");
            sendSpans.Should().NotBeEmpty("should have send spans");

            // Validate receive spans have consumer kind
            foreach (var span in receiveSpans)
            {
                span.GetTag("span.kind").Should().Be("consumer");
                span.GetTag("messaging.system").Should().Be("in-memory");
            }

            // Validate process spans have consumer kind
            foreach (var span in processSpans)
            {
                span.GetTag("span.kind").Should().Be("consumer");
            }

            // Validate send spans have producer kind
            foreach (var span in sendSpans)
            {
                span.GetTag("span.kind").Should().Be("producer");
            }

            // Verify context propagation within MassTransit
            // For in-memory transport, initial publishes start new traces but the
            // receive -> process -> send chain should be connected within each trace
            var traceGroups = massTransitSpans.GroupBy(span => span.TraceId).ToList();
            Output.WriteLine($"Number of distinct traces: {traceGroups.Count}");
            foreach (var group in traceGroups)
            {
                Output.WriteLine($"TraceId {group.Key}: {group.Count()} spans");
            }

            // For proper internal context propagation, we expect traces with multiple spans
            // (receive -> process -> send chains within MassTransit)
            var multiSpanTraces = traceGroups.Where(g => g.Count() > 1).ToList();
            multiSpanTraces.Should().NotBeEmpty("should have traces with multiple connected spans (internal context propagation)");

            // Verify snapshot
            var settings = VerifyHelper.GetSpanVerifierSettings();
            await VerifyHelper.VerifySpans(
                massTransitSpans,
                settings,
                orderSpans: spans => spans
                    .OrderBy(x => x.TraceId)
                    .ThenBy(x => x.Start)
                    .ThenBy(x => x.Name))
                .UseFileName(nameof(MassTransit7Tests));

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.MassTransit);
        }
    }
}
