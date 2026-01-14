// <copyright file="MassTransit7Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

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

            // Verify we have receive and process operations
            var receiveSpans = massTransitSpans.Where(span => span.GetTag("messaging.operation") == "receive").ToList();
            var processSpans = massTransitSpans.Where(span => span.GetTag("messaging.operation") == "process").ToList();

            receiveSpans.Should().NotBeEmpty("should have receive spans");
            processSpans.Should().NotBeEmpty("should have process spans");

            // Validate receive spans have consumer kind and proper tags
            foreach (var span in receiveSpans)
            {
                span.GetTag("span.kind").Should().Be("consumer");
                span.GetTag("messaging.system").Should().Be("in-memory");
                span.Type.Should().Be("queue");
            }

            // Validate process spans have consumer kind
            foreach (var span in processSpans)
            {
                span.GetTag("span.kind").Should().Be("consumer");
            }
        }
    }
}
