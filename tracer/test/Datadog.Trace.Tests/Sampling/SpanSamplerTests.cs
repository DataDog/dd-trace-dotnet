// <copyright file="SpanSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
        private static readonly Span CartCheckoutSpan = new Span(new SpanContext(Id++, Id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "checkout" };

        [Fact]
        public void Constructor_ShouldThrow_WhenNullRulesGiven()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new SpanSampler(null));
            Assert.Equal("rules", exception.ParamName);
        }

        [Fact]
        public void NoMatchingRules_ShouldNot_TagSpan()
        {
            var rule1 = new SpanSamplingRule("serviceName", "operationName", 1.0f, 500.0f);
            var rule2 = new SpanSamplingRule("serviceName2", "operationName2", 1.0f, 500.0f);
            var rules = new List<SpanSamplingRule>() { rule1, rule2 };
            var sampler = new SpanSampler(rules);
            var span = new Span(new SpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };

            Assert.Null(sampler.MakeSamplingDecision(span));

            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));
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
            var span = new Span(new SpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };

            var matchedRule = sampler.MakeSamplingDecision(span);
            Assert.Equal(rule1, matchedRule);
            sampler.AddTags(span, matchedRule);

            Assert.Equal(expectedRuleRate.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Equal(expectedMaxPerSecond.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Equal(expectedSamplingMechanism.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));
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
            var span = new Span(new SpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };

            var matchedRule = sampler.MakeSamplingDecision(span);
            Assert.Equal(rule2, matchedRule);
            sampler.AddTags(span, matchedRule);

            Assert.Equal(expectedRuleRate.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Equal(expectedMaxPerSecond.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Equal(expectedSamplingMechanism.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));
        }

        [Fact]
        public void NoRules_ShouldNot_TagSpan()
        {
            var sampler = new SpanSampler(new List<SpanSamplingRule>());
            var span = new Span(new SpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };

            var matchedRule = sampler.MakeSamplingDecision(span);
            Assert.Null(matchedRule);

            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));
        }

        [Fact]
        public void MaxPerSecond_ShouldNotBeTagged_WhenNotExists()
        {
            var expectedRuleRate = 0.99f;
            var expectedSamplingMechanism = 8; // SamplingMechanism.SpanSamplingRule
            var rule1 = new SpanSamplingRule("service-name", "operation-name", 0.99f);
            var rules = new List<SpanSamplingRule>() { rule1 };
            var sampler = new SpanSampler(rules);
            var span = new Span(new SpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };

            var matchedRule = sampler.MakeSamplingDecision(span);
            Assert.Equal(rule1, matchedRule);
            sampler.AddTags(span, matchedRule);

            Assert.Equal(expectedRuleRate.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Null(span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Equal(expectedSamplingMechanism.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));
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
            var span = new Span(new SpanContext(5, 6, null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };

            var matchedRule = sampler.MakeSamplingDecision(span);
            Assert.Equal(rule1, matchedRule);
            sampler.AddTags(span, matchedRule);

            Assert.Equal(expectedRuleRate.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.RuleRate));
            Assert.Equal(expectedMaxPerSecond.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.MaxPerSecond));
            Assert.Equal(expectedSamplingMechanism.ToString(), span.Tags.GetTag(Tags.SingleSpanSampling.SamplingMechanism));
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
            var allowHalfRule = new SpanSamplingRule("*", "*", 0.5f);
            var rules = new List<SpanSamplingRule>() { allowHalfRule };
            var sampler = new SpanSampler(rules);

            RunSamplerTest(sampler, 500, expectedAutoKeepRate: 0.5f, expectedUserKeepRate: 0.5f, acceptableVariancePercent: 0.1f);
        }

        // TODO rate limiter test? didn't seem to be reliable though

        /// <summary>
        /// Copied from <see cref="TraceSamplerTests.RunSamplerTest(ITraceSampler, int, float, float, float)"/>
        /// </summary>
        private void RunSamplerTest(ISpanSampler sampler, int iterations, float expectedAutoKeepRate, float expectedUserKeepRate, float acceptableVariancePercent)
        {
            var numberOfAutoKeep = 0; // "no match"
            var numberOfUserKeep = 0; // "match"

            for (var i = 0; i < iterations; i++)
            {
                var traceId = SpanIdGenerator.CreateNew();
                var span = GetSpan(traceId);
                var decision = sampler.MakeSamplingDecision(span); // not actually tagging the span

                if (decision is null)
                {
                    numberOfAutoKeep++;
                }
                else
                {
                    numberOfUserKeep++;
                }
            }

            // AUTO_KEEP
            var autoKeepRate = numberOfAutoKeep / (float)iterations;
            var autoKeepRateLowerLimit = expectedAutoKeepRate * (1 - acceptableVariancePercent);
            var autoKeepRateUpperLimit = expectedAutoKeepRate * (1 + acceptableVariancePercent);

            Assert.True(
                autoKeepRate >= autoKeepRateLowerLimit && autoKeepRate <= autoKeepRateUpperLimit,
                $"Sampling AUTO_KEEP rate expected between {autoKeepRateLowerLimit} and {autoKeepRateUpperLimit}, actual rate is {autoKeepRate}.");

            // USER_KEEP (aka MANUAL_KEEP)
            var userKeepRate = numberOfUserKeep / (float)iterations;
            var userKeepRateLowerLimit = expectedUserKeepRate * (1 - acceptableVariancePercent);
            var userKeepRateUpperLimit = expectedUserKeepRate * (1 + acceptableVariancePercent);

            Assert.True(
                userKeepRate >= userKeepRateLowerLimit && userKeepRate <= userKeepRateUpperLimit,
                $"Sampling USER_KEEP rate expected between {userKeepRateLowerLimit} and {userKeepRateUpperLimit}, actual rate is {userKeepRate}.");
        }

        private Span GetSpan(ulong traceId)
        {
            var span = new Span(new SpanContext(traceId, SpanIdGenerator.CreateNew(), null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };
            return span;
        }
    }
}
