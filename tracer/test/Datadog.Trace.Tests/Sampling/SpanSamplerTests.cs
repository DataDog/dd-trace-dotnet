// <copyright file="SpanSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Sampling))]
    public class SpanSamplerTests
    {
        private static readonly ulong Id = 1;
        private static readonly Span CartCheckoutSpan;

        static SpanSamplerTests()
        {
            CartCheckoutSpan = Span.CreateSpan(Span.CreateSpanContext(Id++, Id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now);
            CartCheckoutSpan.OperationName = "checkout";
        }

        [Fact]
        public void Constructor_ShouldThrow_WhenNullRulesGiven()
        {
            var ctor = () => new SpanSampler(null);
            ctor.Should().Throw<ArgumentNullException>().WithParameterName("rules");
        }

        [Fact]
        public void NoMatchingRules_ShouldNot_TagSpan()
        {
            var rule1 = new SpanSamplingRule("serviceName", "operationName", 1.0f, 500.0f);
            var rule2 = new SpanSamplingRule("serviceName2", "operationName2", 1.0f, 500.0f);
            var rules = new List<SpanSamplingRule>() { rule1, rule2 };
            var sampler = new SpanSampler(rules);
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now);
            span.OperationName = "operation-name";

            sampler.MakeSamplingDecision(span).Should().BeFalse();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().BeNull();
        }

        [Fact]
        public void MatchingRule_ButNoSample_ShouldReturnFalse()
        {
            var rule = new SpanSamplingRule("serviceName", "operationName", 0.0f);
            var rules = new List<SpanSamplingRule>() { rule };
            var sampler = new SpanSampler(rules);
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "test"), DateTimeOffset.Now);
            span.OperationName = "test";

            sampler.MakeSamplingDecision(span).Should().BeFalse();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().BeNull();
        }

        [Fact]
        public void FirstMatchingRule_Should_Tag()
        {
            var expectedRuleRate = 1.0f;
            var expectedMaxPerSecond = 500.0f;
            var expectedSamplingMechanism = 8; // SamplingMechanism.SpanSamplingRule
            var rule1 = new SpanSamplingRule("service-name", "operation-name", 1.0f, 500.0f);
            var rule2 = new SpanSamplingRule("service-name", "operation-name", 1.0f, 600.0f); // note different max per second here
            var rules = new List<SpanSamplingRule>() { rule1, rule2 };
            var sampler = new SpanSampler(rules);
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now);
            span.OperationName = "operation-name";

            sampler.MakeSamplingDecision(span).Should().BeTrue();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().Be(expectedRuleRate);
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().Be(expectedMaxPerSecond);
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().Be(expectedSamplingMechanism);
        }

        [Fact]
        public void NonFirstMatchingRule_Should_Tag()
        {
            var expectedRuleRate = 1.0f;
            var expectedMaxPerSecond = 600.0f;
            var expectedSamplingMechanism = 8; // SamplingMechanism.SpanSamplingRule
            var rule1 = new SpanSamplingRule("nomatch", "nomatch", 1.0f, 500.0f);
            var rule2 = new SpanSamplingRule("service-name", "operation-name", 1.0f, 600.0f); // note different max per second here
            var rules = new List<SpanSamplingRule>() { rule1, rule2 };
            var sampler = new SpanSampler(rules);
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now);
            span.OperationName = "operation-name";

            sampler.MakeSamplingDecision(span).Should().BeTrue();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().Be(expectedRuleRate);
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().Be(expectedMaxPerSecond);
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().Be(expectedSamplingMechanism);
        }

        [Fact]
        public void FirstMatchingRule_ShouldTakePriority_AndNotTag()
        {
            var rule1 = new SpanSamplingRule("*", "*", 0.0f); // sample_rate is set to drop all
            var rule2 = new SpanSamplingRule("*", "*", 1.0f);
            var rules = new List<SpanSamplingRule>() { rule1, rule2 };
            var sampler = new SpanSampler(rules);
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now);
            span.OperationName = "operation-name";

            sampler.MakeSamplingDecision(span).Should().BeFalse();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().BeNull();
        }

        [Fact]
        public void NoRules_ShouldNot_TagSpan()
        {
            var sampler = new SpanSampler(Enumerable.Empty<ISpanSamplingRule>());
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now);
            span.OperationName = "operation-name";

            sampler.MakeSamplingDecision(span).Should().BeFalse();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().BeNull();
        }

        [Fact]
        public void SingleCharacterReplacement_ShouldTagSpan_WhenMatches()
        {
            var rule = new SpanSamplingRule("se?v?ce", "o?erat?o?", maxPerSecond: 1000.0f);
            var sampler = new SpanSampler(new List<SpanSamplingRule>() { rule });
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "service"), DateTimeOffset.Now);
            span.OperationName = "operation";

            sampler.MakeSamplingDecision(span).Should().BeTrue();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().NotBeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().NotBeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().NotBeNull();
        }

        [Fact]
        public void SingleCharacterReplacement_ShouldNotTagSpan_WhenNotMatches()
        {
            var rule = new SpanSamplingRule("se?v?ce", "o?erat?o?", maxPerSecond: 1000.0f);
            var sampler = new SpanSampler(new List<SpanSamplingRule>() { rule });
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "serrvice"), DateTimeOffset.Now);
            span.OperationName = "opperation";

            sampler.MakeSamplingDecision(span).Should().BeFalse();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().BeNull();
        }

        [Fact]
        public void MaxPerSecond_ShouldNotBeTagged_WhenNotExists()
        {
            var expectedRuleRate = 0.99f;
            var expectedSamplingMechanism = 8; // SamplingMechanism.SpanSamplingRule
            var rule1 = new SpanSamplingRule("service-name", "operation-name", 0.99f);
            var rules = new List<SpanSamplingRule>() { rule1 };
            var sampler = new SpanSampler(rules);
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now);
            span.OperationName = "operation-name";

            sampler.MakeSamplingDecision(span).Should().BeTrue();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().Be(expectedRuleRate);
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().BeNull();
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().Be(expectedSamplingMechanism);
        }

        [Fact]
        public void Tags_ShouldBe_Correct()
        {
            var expectedRuleRate = 0.99f;
            var expectedMaxPerSecond = 500.0f;
            var expectedSamplingMechanism = 8; // SamplingMechanism.SpanSamplingRule
            var rule1 = new SpanSamplingRule("service-name", "operation-name", 0.99f, 500.0f);
            var rules = new List<SpanSamplingRule>() { rule1 };
            var sampler = new SpanSampler(rules);
            var span = Span.CreateSpan(Span.CreateSpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now);
            span.OperationName = "operation-name";

            sampler.MakeSamplingDecision(span).Should().BeTrue();

            span.Tags.GetMetric(Metrics.SingleSpanSampling.RuleRate).Should().Be(expectedRuleRate);
            span.Tags.GetMetric(Metrics.SingleSpanSampling.MaxPerSecond).Should().Be(expectedMaxPerSecond);
            span.Tags.GetMetric(Metrics.SingleSpanSampling.SamplingMechanism).Should().Be(expectedSamplingMechanism);
        }

        [Fact]
        public void MultipleRule_AllowNone()
        {
            var allowNoneRule = new SpanSamplingRule("*", "*", 0.0f); // this rule comes before allow all, so it has priority
            var allowAllRule = new SpanSamplingRule("*", "*");
            var rules = new List<SpanSamplingRule>() { allowNoneRule, allowAllRule };
            var sampler = new SpanSampler(rules);

            RunSamplerTest(sampler, 500, expectedAutoKeepRate: 1, expectedUserKeepRate: 0, acceptableVariancePercent: 0);
        }

        [Fact]
        public void Allow_All()
        {
            var allowAllRule = new SpanSamplingRule("*", "*");
            var rules = new List<SpanSamplingRule>() { allowAllRule };
            var sampler = new SpanSampler(rules);

            RunSamplerTest(sampler, 500, expectedAutoKeepRate: 0, expectedUserKeepRate: 1, acceptableVariancePercent: 0);
        }

        [Fact]
        public void Allow_None_SamplingRate()
        {
            var allowNoneRule = new SpanSamplingRule("*", "*", 0.0f);
            var rules = new List<SpanSamplingRule>() { allowNoneRule };
            var sampler = new SpanSampler(rules);

            RunSamplerTest(sampler, 500, expectedAutoKeepRate: 1, expectedUserKeepRate: 0, acceptableVariancePercent: 0);
        }

        [Fact]
        public void Allow_Half_SamplingRate()
        {
            var allowHalfRule = new SpanSamplingRule("service*", "operation?name", 0.5f);
            var rules = new List<SpanSamplingRule>() { allowHalfRule };
            var sampler = new SpanSampler(rules);

            RunSamplerTest(sampler, 500, expectedAutoKeepRate: 0.5f, expectedUserKeepRate: 0.5f, acceptableVariancePercent: 0.2f);
        }

        /// <summary>
        /// Copied from <see cref="TraceSamplerTests.RunSamplerTest(ITraceSampler, int, float, float, float)"/>
        /// </summary>
        private void RunSamplerTest(ISpanSampler sampler, int iterations, float expectedAutoKeepRate, float expectedUserKeepRate, float acceptableVariancePercent)
        {
            var numberOfAutoKeep = 0; // "no match"
            var numberOfUserKeep = 0; // "match"

            for (var i = 0; i < iterations; i++)
            {
                var traceId = RandomIdGenerator.Shared.NextSpanId();
                var span = GetSpan(traceId);
                var sampled = sampler.MakeSamplingDecision(span);

                if (sampled)
                {
                    numberOfUserKeep++;
                }
                else
                {
                    numberOfAutoKeep++;
                }
            }

                        // AUTO_KEEP
            var autoKeepRate = numberOfAutoKeep / (float)iterations;
            var autoKeepPrecision = expectedAutoKeepRate * acceptableVariancePercent;
            autoKeepRate.Should().BeApproximately(expectedAutoKeepRate, autoKeepPrecision, $"Sampling AUTO_KEEP rate should be approximately expected value.");

            // USER_KEEP (aka MANUAL_KEEP)
            var userKeepRate = numberOfUserKeep / (float)iterations;
            var userKeepPrecision = expectedUserKeepRate * acceptableVariancePercent;
            userKeepRate.Should().BeApproximately(expectedUserKeepRate, userKeepPrecision, $"Sampling USER_KEEP rate should be approximately expected value.");
        }

        private Span GetSpan(ulong traceId)
        {
            var span = Span.CreateSpan(Span.CreateSpanContext(traceId, RandomIdGenerator.Shared.NextSpanId(), null, serviceName: "service-name"), DateTimeOffset.Now);
            span.OperationName = "operation-name";
            return span;
        }
    }
}
