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
        private readonly AgentWriter _writer;
        private readonly MockApi _testApi;

        public SpanTagTests()
        {
            _testApi = new MockApi();
            var matchAllRule = "[{\"service\":\"*\", \"name\":\"*\", \"sample_rate\":1.0, \"max_per_second\":1000.0}]";
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.SpanSamplingRules, matchAllRule } });
            _writer = new AgentWriter(_testApi, statsAggregator: null, statsd: null);
            _tracer = new Tracer(settings, _writer, sampler: null, scopeManager: null, statsd: null);
        }

        [Fact]
        public void SpanSampler_ShouldNotAddTags_OnSpanClose_ForKeptTrace()
        {
            var expectedRuleRate = 1.0f;
            var expectedMaxPerSecond = 1000.0f;
            var expectedSamplingMechanism = 8;

            using (var scope = _tracer.StartActive("root"))
            {
            }

            var trace = _testApi.Wait();
            trace.Should().HaveCount(1);
            trace[0].Should().HaveCount(1);

            var span = trace[0].Single();
            span.Metrics.Should().NotContain(Metrics.SingleSpanSampling.RuleRate, expectedRuleRate);
            span.Metrics.Should().NotContain(Metrics.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
            span.Metrics.Should().NotContain(Metrics.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);
        }

        [Fact]
        public void SpanSampler_ShouldTag_OnSpanFinish()
        {
            var expectedRuleRate = 1.0f;
            var expectedMaxPerSecond = 1000.0f;
            var expectedSamplingMechanism = 8;

            using (var scope = _tracer.StartActive("root"))
            {
                // drop it
                ((SpanContext)scope.Span.Context).TraceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            }

            var trace = _testApi.Wait();
            trace.Should().HaveCount(1);
            trace[0].Should().HaveCount(1);

            var span = trace[0].Single();
            span.Metrics.Should().Contain(Metrics.SingleSpanSampling.RuleRate, expectedRuleRate);
            span.Metrics.Should().Contain(Metrics.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
            span.Metrics.Should().Contain(Metrics.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);
        }

        [Fact]
        public void SpanSampler_ShouldTagMultiple_OnSpanFinish()
        {
            var expectedRuleRate = 1.0f;
            var expectedMaxPerSecond = 1000.0f;
            var expectedSamplingMechanism = 8;

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
            rootSpan.Metrics.Should().Contain(Metrics.SingleSpanSampling.RuleRate, expectedRuleRate);
            rootSpan.Metrics.Should().Contain(Metrics.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
            rootSpan.Metrics.Should().Contain(Metrics.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);

            // assert child spans have span sampling tags
            var childSpans = traces[0].Where(s => s.ParentId is not null and not 0);

            foreach (var span in childSpans)
            {
                span.Metrics.Should().Contain(Metrics.SingleSpanSampling.RuleRate, expectedRuleRate);
                span.Metrics.Should().Contain(Metrics.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
                span.Metrics.Should().Contain(Metrics.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);
            }
        }

        [Fact]
        public void SpanSampler_ShouldTagMultiple_OnSpanFinish_WhenSamplingPriorityChanges()
        {
            var expectedRuleRate = 1.0f;
            var expectedMaxPerSecond = 1000.0f;
            var expectedSamplingMechanism = 8;

            using (var rootScope = _tracer.StartActive("root"))
            {
                // keep it (should be kept by default, but enforcing it in this test)
                ((SpanContext)rootScope.Span.Context).TraceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Manual);

                using (var childScope = _tracer.StartActive("child1"))
                {
                }

                using (var childScope = _tracer.StartActive("child2"))
                {
                }

                // drop it
                ((SpanContext)rootScope.Span.Context).TraceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            }

            var traces = _testApi.Wait();
            traces.Should().HaveCount(1); // 1 trace...
            traces[0].Should().HaveCount(3); // ...with 3 spans

            var rootSpan = traces[0].SingleOrDefault(s => s.ParentId is null or 0)!;
            rootSpan.Should().NotBeNull();

            // assert that root span has the span sampling tags
            rootSpan.Metrics.Should().Contain(Metrics.SingleSpanSampling.RuleRate, expectedRuleRate);
            rootSpan.Metrics.Should().Contain(Metrics.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
            rootSpan.Metrics.Should().Contain(Metrics.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);

            // assert child spans have span sampling tags
            var childSpans = traces[0].Where(s => s.ParentId is not null and not 0);

            foreach (var span in childSpans)
            {
                span.Metrics.Should().Contain(Metrics.SingleSpanSampling.RuleRate, expectedRuleRate);
                span.Metrics.Should().Contain(Metrics.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
                span.Metrics.Should().Contain(Metrics.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);
            }
        }

        [Fact]
        public void SpanSampler_ShouldNotTag_WhenSpansAreKept()
        {
            var expectedRuleRate = 1.0f;
            var expectedMaxPerSecond = 1000.0f;
            var expectedSamplingMechanism = 8;

            using (var rootScope = _tracer.StartActive("root"))
            {
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
            rootSpan.Metrics.Should().NotContain(Metrics.SingleSpanSampling.RuleRate, expectedRuleRate);
            rootSpan.Metrics.Should().NotContain(Metrics.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
            rootSpan.Metrics.Should().NotContain(Metrics.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);

            // assert child spans have span sampling tags
            var childSpans = traces[0].Where(s => s.ParentId is not null and not 0);

            foreach (var span in childSpans)
            {
                span.Metrics.Should().NotContain(Metrics.SingleSpanSampling.RuleRate, expectedRuleRate);
                span.Metrics.Should().NotContain(Metrics.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
                span.Metrics.Should().NotContain(Metrics.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);
            }
        }

        [Fact]
        public void SpanSampler_ShouldNotTag_WhenSamplingPriority_IsNull()
        {
            var expectedRuleRate = 1.0f;
            var expectedMaxPerSecond = 1000.0f;
            var expectedSamplingMechanism = 8;

            var spanContext = new SpanContext(4, 5, samplingPriority: null, serviceName: "serviceName");
            var span = new Span(spanContext, DateTimeOffset.Now) { OperationName = "test" };
            var spans = new Span[1];
            spans[0] = span;
            _writer.WriteTrace(new ArraySegment<Span>(spans));
            var trace = _testApi.Wait();
            trace.Should().HaveCount(1);
            trace[0].Should().HaveCount(1);

            var writtenSpan = trace[0].Single();
            writtenSpan.Metrics.Should().NotContain(Metrics.SingleSpanSampling.RuleRate, expectedRuleRate);
            writtenSpan.Metrics.Should().NotContain(Metrics.SingleSpanSampling.MaxPerSecond, expectedMaxPerSecond);
            writtenSpan.Metrics.Should().NotContain(Metrics.SingleSpanSampling.SamplingMechanism, expectedSamplingMechanism);
        }
    }
}
