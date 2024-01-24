// <copyright file="CustomSamplingRuleGlobTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class CustomSamplingRuleGlobTests
    {
        [Fact]
        public void Constructs_ZeroRateOnly_From_Config_String()
        {
            const string config = """[{"sample_rate":0}]""";
            VerifyRate(config, 0f);
            VerifySingleRule(config, TestSpans.CartCheckoutSpan, true);
            VerifySingleRule(config, TestSpans.AddToCartSpan, true);
            VerifySingleRule(config, TestSpans.ShippingAuthSpan, true);
            VerifySingleRule(config, TestSpans.ShippingRevertSpan, true);
            VerifySingleRule(config, TestSpans.RequestShippingSpan, true);
        }

        [Fact]
        public void Constructs_CartOnlyRule_From_Config_String()
        {
            const string config = """[{"sample_rate":0.3, "service":"shopping-cart*"}]""";
            VerifyRate(config, 0.3f);
            VerifySingleRule(config, TestSpans.CartCheckoutSpan, true);
            VerifySingleRule(config, TestSpans.AddToCartSpan, true);
            VerifySingleRule(config, TestSpans.ShippingAuthSpan, false);
            VerifySingleRule(config, TestSpans.ShippingRevertSpan, false);
            VerifySingleRule(config, TestSpans.RequestShippingSpan, false);
        }

        [Fact]
        public void Constructs_AuthOperationRule_From_Config_String()
        {
            const string config = """[{"sample_rate":0.5, "name":"auth*"}]""";
            VerifyRate(config, 0.5f);
            VerifySingleRule(config, TestSpans.CartCheckoutSpan, false);
            VerifySingleRule(config, TestSpans.AddToCartSpan, false);
            VerifySingleRule(config, TestSpans.ShippingAuthSpan, true);
            VerifySingleRule(config, TestSpans.ShippingRevertSpan, true);
            VerifySingleRule(config, TestSpans.RequestShippingSpan, false);
        }

        [Fact]
        public void Constructs_With_ResourceName()
        {
            const string config = """[{ "sample_rate":0.3, resource: "/api/v1/*" }]""";
            var rule = CustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob).Single();

            var matchingSpan = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now) { ResourceName = "/api/v1/user/123" };
            var nonMatchingSpan = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now) { ResourceName = "/api/v2/user/123" };

            VerifyRate(rule, 0.3f);
            VerifySingleRule(rule, matchingSpan, isMatch: true);
            VerifySingleRule(rule, nonMatchingSpan, isMatch: false);
        }

        [Fact]
        public void Constructs_With_Tags()
        {
            const string config = """[{ "sample_rate":0.3, tags: { "http.method": "GE?", "http.status_code": "200" } }]""";
            var rule = CustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob).Single();

            var matchingSpan = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            matchingSpan.SetTag("http.method", "GET");
            matchingSpan.SetMetric("http.status_code", 200);

            var nonMatchingSpan1 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            nonMatchingSpan1.SetTag("http.method", "GET");

            var nonMatchingSpan2 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            nonMatchingSpan2.SetTag("http.method", "POST");

            VerifyRate(rule, 0.3f);
            VerifySingleRule(rule, matchingSpan, isMatch: true);
            VerifySingleRule(rule, nonMatchingSpan1, isMatch: false);
            VerifySingleRule(rule, nonMatchingSpan2, isMatch: false);
        }

        [Fact]
        public void Constructs_All_Expected_From_Config_String()
        {
            const string config = """
                                  [
                                    {"sample_rate":0.5, "service":"*cart*"},
                                    {"sample_rate":1, "service":"*shipping*", "name":"authorize"},
                                    {"sample_rate":0.1, "service":"*shipping*"},
                                    {"sample_rate":0.05}
                                  ]
                                  """;

            var rules = CustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob).ToArray();
            rules.Should().HaveCount(4);

            var cartRule = rules[0];
            cartRule.GetSamplingRate(TestSpans.CartCheckoutSpan).Should().Be(0.5f);

            VerifySingleRule(cartRule, TestSpans.CartCheckoutSpan, true);
            VerifySingleRule(cartRule, TestSpans.AddToCartSpan, true);
            VerifySingleRule(cartRule, TestSpans.ShippingAuthSpan, false);
            VerifySingleRule(cartRule, TestSpans.ShippingRevertSpan, false);
            VerifySingleRule(cartRule, TestSpans.RequestShippingSpan, false);

            var shippingAuthRule = rules[1];
            Assert.Equal(expected: 1f, actual: shippingAuthRule.GetSamplingRate(TestSpans.CartCheckoutSpan));

            VerifySingleRule(shippingAuthRule, TestSpans.CartCheckoutSpan, false);
            VerifySingleRule(shippingAuthRule, TestSpans.AddToCartSpan, false);
            VerifySingleRule(shippingAuthRule, TestSpans.ShippingAuthSpan, true);
            VerifySingleRule(shippingAuthRule, TestSpans.ShippingRevertSpan, false);
            VerifySingleRule(shippingAuthRule, TestSpans.RequestShippingSpan, false);

            var fallbackShippingRule = rules[2];
            Assert.Equal(expected: 0.1f, actual: fallbackShippingRule.GetSamplingRate(TestSpans.CartCheckoutSpan));

            VerifySingleRule(fallbackShippingRule, TestSpans.CartCheckoutSpan, false);
            VerifySingleRule(fallbackShippingRule, TestSpans.AddToCartSpan, false);
            VerifySingleRule(fallbackShippingRule, TestSpans.ShippingAuthSpan, true);
            VerifySingleRule(fallbackShippingRule, TestSpans.ShippingRevertSpan, true);
            VerifySingleRule(fallbackShippingRule, TestSpans.RequestShippingSpan, true);

            var fallbackRule = rules[3];
            Assert.Equal(expected: 0.05f, actual: fallbackRule.GetSamplingRate(TestSpans.CartCheckoutSpan));

            VerifySingleRule(fallbackRule, TestSpans.CartCheckoutSpan, true);
            VerifySingleRule(fallbackRule, TestSpans.AddToCartSpan, true);
            VerifySingleRule(fallbackRule, TestSpans.ShippingAuthSpan, true);
            VerifySingleRule(fallbackRule, TestSpans.ShippingRevertSpan, true);
            VerifySingleRule(fallbackRule, TestSpans.RequestShippingSpan, true);
        }

        [Fact]
        public void RuleShouldBeCaseInsensitive()
        {
            var config = "[{\"sample_rate\":0.5, \"service\":\"SHOPPING-cart-service\", \"name\":\"CHECKOUT\"}]";
            var rule = CustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob).Single();
            VerifySingleRule(rule, TestSpans.CartCheckoutSpan, true);
        }

        [Theory]
        [InlineData(@"""rate:0.5, ""name"":""auth*""}]", 0)] // missing closing double quote in "rate"
        [InlineData("""[{"name":"wat"}]""", 0)] // missing "sample_rate"
        [InlineData("""[{"sample_rate":0.3, "service":"["}]""", 1)] // invalid regex, but valid glob
        [InlineData("""[{"sample_rate":0.3, "name":"["}]""", 1)] // invalid regex, but valid glob
        public void Malformed_Rules_Do_Not_Register_Or_Crash(string ruleConfig, int count)
        {
            var rules = CustomSamplingRule.BuildFromConfigurationString(ruleConfig, SamplingRulesFormat.Glob).ToArray();
            rules.Should().HaveCount(count);
        }

        private static void VerifyRate(string config, float expectedRate)
        {
            var rule = CustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob).Single();
            VerifyRate(rule, expectedRate);
        }

        private static void VerifyRate(ISamplingRule rule, float expectedRate)
        {
            rule.GetSamplingRate(TestSpans.CartCheckoutSpan).Should().Be(expectedRate);
        }

        private static void VerifySingleRule(string config, Span span, bool isMatch)
        {
            var rule = CustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob).Single();
            VerifySingleRule(rule, span, isMatch);
        }

        private static void VerifySingleRule(ISamplingRule rule, Span span, bool isMatch)
        {
            rule.IsMatch(span).Should().Be(isMatch);
        }
    }
}
