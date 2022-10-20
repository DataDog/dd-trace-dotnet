// <copyright file="SpanTagTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class SpanTagTests
    {
        private readonly Tracer _tracer;
        private readonly MockApi _testApi;

        public SpanTagTests()
        {
            _testApi = new MockApi();
            var matchAllRule = "[{\"service\":\"*\", \"name\":\"*\", \"sample_rate\":1.0, \"max_per_second\":1000.0}]";
            var settings = new TracerSettings() { SpanSamplingRules = matchAllRule };
            var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: null);
            // a spanSampler should be generated due to the TracerSettings containing the SpanSamplingRules
            _tracer = new Tracer(settings, agentWriter, sampler: null, spanSampler: null, scopeManager: null, statsd: null);
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

        [Fact]
        public void SpanSampler_ShouldTag_OnSpanFinish()
        {
            var expectedRuleRate = "1";
            var expectedMaxPerSecond = "1000";
            var expectedSamplingMechanism = "8";

            using (var scope = _tracer.StartActive("root"))
            {
                // drop it
                ((SpanContext)scope.Span.Context).TraceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            }

            var trace = _testApi.Wait();
            trace.Should().HaveCount(1);
            trace[0].Should().HaveCount(1);

            var span = trace[0].Single();
            span.Tags.Should().Contain(Tags.SingleSpanSampling.RuleRate, expectedRuleRate);
            span.Tags.Should().Contain(Tags.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
            span.Tags.Should().Contain(Tags.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);
        }

        [Fact]
        public void SpanSampler_ShouldTagMultiple_OnSpanFinish()
        {
            var expectedRuleRate = "1";
            var expectedMaxPerSecond = "1000";
            var expectedSamplingMechanism = "8";

            using (var rootScope = _tracer.StartActive("root"))
            {
                // drop it
                ((SpanContext)rootScope.Span.Context).TraceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);

                using (var childScope = _tracer.StartActive("child1"))
                {
                }

                using (var childScope = _tracer.StartActive("child2"))
                {
                }
            }

            var traces = _testApi.Wait();
            traces.Should().HaveCount(1); // 1 trace...
            traces[0].Should().HaveCount(3); // ...with 3 spans

            var rootSpan = traces[0].SingleOrDefault(s => s.ParentId is null or 0)!;
            rootSpan.Should().NotBeNull();

            // assert that root span has the span sampling tags
            rootSpan.Tags.Should().Contain(Tags.SingleSpanSampling.RuleRate, expectedRuleRate);
            rootSpan.Tags.Should().Contain(Tags.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
            rootSpan.Tags.Should().Contain(Tags.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);

            // assert child spans have span sampling tags
            var childSpans = traces[0].Where(s => s.ParentId is not null and not 0);

            foreach (var span in childSpans)
            {
                span.Tags.Should().Contain(Tags.SingleSpanSampling.RuleRate, expectedRuleRate);
                span.Tags.Should().Contain(Tags.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
                span.Tags.Should().Contain(Tags.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);
            }
        }
    }
}
