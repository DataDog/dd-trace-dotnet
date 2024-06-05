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
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

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
        public void Constructs_With_ResourceName_Local()
        {
            const string config = """[{ "sample_rate":0.3, resource: "/api/v1/*" }]""";
            var rule = LocalCustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob, Timeout).Single();

            var matchingSpan = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now) { ResourceName = "/api/v1/user/123" };
            var nonMatchingSpan = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now) { ResourceName = "/api/v2/user/123" };

            VerifyRate(rule, 0.3f);
            VerifySingleRule(rule, matchingSpan, isMatch: true);
            VerifySingleRule(rule, nonMatchingSpan, isMatch: false);
        }

        [Fact]
        public void Constructs_With_ResourceName_Remote()
        {
            const string config = """[{ "sample_rate":0.3, resource: "/api/v1/*" }]""";
            var rule = RemoteCustomSamplingRule.BuildFromConfigurationString(config, Timeout).Single();

            var matchingSpan = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now) { ResourceName = "/api/v1/user/123" };
            var nonMatchingSpan = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now) { ResourceName = "/api/v2/user/123" };

            VerifyRate(rule, 0.3f);
            VerifySingleRule(rule, matchingSpan, isMatch: true);
            VerifySingleRule(rule, nonMatchingSpan, isMatch: false);
        }

        [Fact]
        public void Constructs_With_Tags_Local()
        {
            const string config = """[{ "sample_rate":0.3, tags: { "http.method": "GE?", "http.status_code": "200", "balance": "*" } }]""";
            var rule = LocalCustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob, Timeout).Single();

            var matchingSpan1 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            matchingSpan1.SetTag("http.method", "GET");
            matchingSpan1.SetMetric("http.status_code", 200); // matches as string or int
            matchingSpan1.SetMetric("balance", 12);           // "*" matches ints or floats

            var matchingSpan2 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            matchingSpan2.SetTag("http.method", "GEX");      // "GE?" matches any single character
            matchingSpan2.SetTag("http.status_code", "200"); // matches as string or int
            matchingSpan2.SetMetric("balance", 12.34);       // "*" matches ints or floats

            // missing "http.status_code" tag
            var nonMatchingSpan1 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            nonMatchingSpan1.SetTag("http.method", "GET");
            nonMatchingSpan1.SetMetric("balance", 12.34);

            var nonMatchingSpan2 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            nonMatchingSpan2.SetTag("http.method", "POST"); // no match
            nonMatchingSpan2.SetMetric("http.status_code", 200);
            nonMatchingSpan2.SetMetric("balance", 12.34);

            VerifyRate(rule, 0.3f);
            VerifySingleRule(rule, matchingSpan1, isMatch: true);
            VerifySingleRule(rule, matchingSpan2, isMatch: true);
            VerifySingleRule(rule, nonMatchingSpan1, isMatch: false);
            VerifySingleRule(rule, nonMatchingSpan2, isMatch: false);
        }

        [Fact]
        public void Constructs_With_Tags_Remote()
        {
            const string config = """[{ "sample_rate":0.3, "tags": [{ "key":"http.method", "value_glob":"GE?" }, { "key":"http.status_code", "value_glob":"200"}, { "key":"balance", "value_glob":"*" }] }]""";
            var rule = RemoteCustomSamplingRule.BuildFromConfigurationString(config, Timeout).Single();

            var matchingSpan1 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            matchingSpan1.SetTag("http.method", "GET");
            matchingSpan1.SetMetric("http.status_code", 200); // matches as string or int
            matchingSpan1.SetMetric("balance", 12);           // "*" matches ints or floats

            var matchingSpan2 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            matchingSpan2.SetTag("http.method", "GEX");      // "GE?" matches any single character
            matchingSpan2.SetTag("http.status_code", "200"); // matches as string or int
            matchingSpan2.SetMetric("balance", 12.34);       // "*" matches ints or floats

            // missing "http.status_code" tag
            var nonMatchingSpan1 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            nonMatchingSpan1.SetTag("http.method", "GET");
            nonMatchingSpan1.SetMetric("balance", 12.34);

            var nonMatchingSpan2 = new Span(new SpanContext(1, 1, serviceName: "foo"), DateTimeOffset.Now);
            nonMatchingSpan2.SetTag("http.method", "POST"); // no match
            nonMatchingSpan2.SetMetric("http.status_code", 200);
            nonMatchingSpan2.SetMetric("balance", 12.34);

            VerifyRate(rule, 0.3f);
            VerifySingleRule(rule, matchingSpan1, isMatch: true);
            VerifySingleRule(rule, matchingSpan2, isMatch: true);
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

            var rules = LocalCustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob, Timeout).ToArray();
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
            var rule = LocalCustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob, Timeout).Single();
            VerifySingleRule(rule, TestSpans.CartCheckoutSpan, true);
        }

        [Theory]
        [InlineData(@"""rate:0.5, ""name"":""auth*""}]", 0)] // missing closing double quote in "rate"
        [InlineData("""[{"name":"wat"}]""", 0)] // missing "sample_rate"
        [InlineData("""[{"sample_rate":0.3, "service":"["}]""", 1)] // invalid regex, but valid glob
        [InlineData("""[{"sample_rate":0.3, "name":"["}]""", 1)] // invalid regex, but valid glob
        public void Malformed_Rules_Do_Not_Register_Or_Crash(string ruleConfig, int count)
        {
            var rules = LocalCustomSamplingRule.BuildFromConfigurationString(ruleConfig, SamplingRulesFormat.Glob, Timeout).ToArray();
            rules.Should().HaveCount(count);
        }

        private static void VerifyRate(string config, float expectedRate)
        {
            var rule = LocalCustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob, Timeout).Single();
            VerifyRate(rule, expectedRate);
        }

        private static void VerifyRate(ISamplingRule rule, float expectedRate)
        {
            rule.GetSamplingRate(TestSpans.CartCheckoutSpan).Should().Be(expectedRate);
        }

        private static void VerifySingleRule(string config, Span span, bool isMatch)
        {
            var rule = LocalCustomSamplingRule.BuildFromConfigurationString(config, SamplingRulesFormat.Glob, Timeout).Single();
            VerifySingleRule(rule, span, isMatch);
        }

        private static void VerifySingleRule(ISamplingRule rule, Span span, bool isMatch)
        {
            rule.IsMatch(span).Should().Be(isMatch);
        }
    }
}
