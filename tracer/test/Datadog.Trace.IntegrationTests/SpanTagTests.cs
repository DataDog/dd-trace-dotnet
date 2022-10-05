// <copyright file="SpanTagTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class SpanTagTests
    {
        private readonly Tracer _tracer;
        private readonly TestApi _testApi;

        public SpanTagTests()
        {
            _testApi = new TestApi();
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
            var span = new Span(new SpanContext(5, 6, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "checkout" };
            var span2 = new Span(new SpanContext(7, 8, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "checkout" };
            // mechanism not important, but we've decided to drop the trace
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            traceContext.AddSpan(span2);
            traceContext.CloseSpan(span2);

            traceContext.AddSpan(span);
            traceContext.CloseSpan(span);

            Assert.Equal("1", span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Equal("1000", span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Equal("8", span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));

            Assert.Equal("1", span2.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Equal("1000", span2.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Equal("8", span2.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));
        }

        [Fact]
        public void SpanSampler_ShouldNotAddTags_OnSpanClose_ForKeptTrace()
        {
            var traceContext = new TraceContext(_tracer);
            var span = new Span(new SpanContext(5, 6, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "checkout" };
            var span2 = new Span(new SpanContext(7, 8, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "checkout" };
            // mechanism not important, but we've decided to drop the trace
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Manual);
            traceContext.AddSpan(span2);
            traceContext.CloseSpan(span2);

            traceContext.AddSpan(span);
            traceContext.CloseSpan(span);

            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));

            Assert.Null(span2.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Null(span2.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Null(span2.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));
        }
    }
}
