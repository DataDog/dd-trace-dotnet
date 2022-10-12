// <copyright file="SpanTagTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class SpanTagTests
    {
        private readonly Tracer _tracer;

        public SpanTagTests()
        {
            var matchAllRule = "[{\"service\":\"*\", \"name\":\"*\", \"sample_rate\":1.0, \"max_per_second\":1000.0}]";
            var settings = new TracerSettings() { SpanSamplingRules = matchAllRule };
            // a spanSampler should be generated due to the TracerSettings containing the SpanSamplingRules
            _tracer = new Tracer(settings, null, sampler: null, spanSampler: null, scopeManager: null, statsd: null);
        }

        [Fact]
        public void SpanSampler_ShouldAddTags_OnSpanClose_ForDroppedTrace()
        {
            var traceContext = new TraceContext(_tracer);
            var expectedRuleRate = "1";
            var expectedMaxPerSecond = "1000";
            var expectedSamplingMechanism = "8";
            var span = new Span(new SpanContext(5, 6, null, serviceName: "service"), DateTimeOffset.Now) { OperationName = "operation" };
            var span2 = new Span(new SpanContext(5, 7, null, serviceName: "service"), DateTimeOffset.Now) { OperationName = "operation" };
            // mechanism not important, but we've decided to drop the trace
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            traceContext.AddSpan(span);
            traceContext.AddSpan(span2);

            traceContext.CloseSpan(span);
            traceContext.CloseSpan(span2);

            span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate).Should().Be(expectedRuleRate);
            span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond).Should().Be(expectedMaxPerSecond);
            span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism).Should().Be(expectedSamplingMechanism);

            span2.Tags.GetTag(Tags.SingleSpanSampling.RuleRate).Should().Be(expectedRuleRate);
            span2.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond).Should().Be(expectedMaxPerSecond);
            span2.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism).Should().Be(expectedSamplingMechanism);
        }

        [Fact]
        public void SpanSampler_ShouldNotAddTags_OnSpanClose_ForKeptTrace()
        {
            var traceContext = new TraceContext(_tracer);
            var span = new Span(new SpanContext(5, 6, null, serviceName: "service"), DateTimeOffset.Now) { OperationName = "operation" };
            var span2 = new Span(new SpanContext(5, 7, null, serviceName: "service"), DateTimeOffset.Now) { OperationName = "operation" };
            // mechanism not important, but we've decided to keep the trace
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Manual);
            traceContext.AddSpan(span);
            traceContext.AddSpan(span2);

            traceContext.CloseSpan(span);
            traceContext.CloseSpan(span2);

            span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate).Should().BeNull();
            span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond).Should().BeNull();
            span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism).Should().BeNull();

            span2.Tags.GetTag(Tags.SingleSpanSampling.RuleRate).Should().BeNull();
            span2.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond).Should().BeNull();
            span2.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism).Should().BeNull();
        }
    }
}
