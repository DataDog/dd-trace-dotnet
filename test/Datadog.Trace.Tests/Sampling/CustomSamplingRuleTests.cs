using System;
using System.IO;
using System.Linq;
using Datadog.Trace.Sampling;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class CustomSamplingRuleTests
    {
        private static readonly ulong Id = 1;
        private static readonly Span CartCheckoutSpan = new SpanImplementation(new SpanContext(Id++, Id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "checkout" };
        private static readonly Span AddToCartSpan = new SpanImplementation(new SpanContext(Id++, Id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "cart-add" };
        private static readonly Span ShippingAuthSpan = new SpanImplementation(new SpanContext(Id++, Id++, null, serviceName: "shipping-auth-service"), DateTimeOffset.Now) { OperationName = "authorize" };
        private static readonly Span ShippingRevertSpan = new SpanImplementation(new SpanContext(Id++, Id++, null, serviceName: "shipping-auth-service"), DateTimeOffset.Now) { OperationName = "authorize-revert" };
        private static readonly Span RequestShippingSpan = new SpanImplementation(new SpanContext(Id++, Id++, null, serviceName: "request-shipping"), DateTimeOffset.Now) { OperationName = "submit" };

        [Fact]
        public void Constructs_ZeroRateOnly_From_Config_String()
        {
            var config = "[{\"sample_rate\":0}]";
            VerifyRate(config, 0f);
            VerifySingleRule(config, CartCheckoutSpan, true);
            VerifySingleRule(config, AddToCartSpan, true);
            VerifySingleRule(config, ShippingAuthSpan, true);
            VerifySingleRule(config, ShippingRevertSpan, true);
            VerifySingleRule(config, RequestShippingSpan, true);
        }

        [Fact]
        public void Constructs_CartOnlyRule_From_Config_String()
        {
            var config = "[{\"sample_rate\":0.3, \"service\":\"shopping-cart.*\"}]";
            VerifyRate(config, 0.3f);
            VerifySingleRule(config, CartCheckoutSpan, true);
            VerifySingleRule(config, AddToCartSpan, true);
            VerifySingleRule(config, ShippingAuthSpan, false);
            VerifySingleRule(config, ShippingRevertSpan, false);
            VerifySingleRule(config, RequestShippingSpan, false);
        }

        [Fact]
        public void Constructs_AuthOperationRule_From_Config_String()
        {
            var config = "[{\"sample_rate\":0.5, \"name\":\"auth.*\"}]";
            VerifyRate(config, 0.5f);
            VerifySingleRule(config, CartCheckoutSpan, false);
            VerifySingleRule(config, AddToCartSpan, false);
            VerifySingleRule(config, ShippingAuthSpan, true);
            VerifySingleRule(config, ShippingRevertSpan, true);
            VerifySingleRule(config, RequestShippingSpan, false);
        }

        [Fact]
        public void Constructs_All_Expected_From_Config_String()
        {
            var config = "[{\"sample_rate\":0.5, \"service\":\".*cart.*\"}, {\"sample_rate\":1, \"service\":\".*shipping.*\", \"name\":\"authorize\"}, {\"sample_rate\":0.1, \"service\":\".*shipping.*\"}, {\"sample_rate\":0.05}]";
            var rules = CustomSamplingRule.BuildFromConfigurationString(config).ToArray();

            var cartRule = rules[0];
            Assert.Equal(expected: 0.5f, actual: cartRule.GetSamplingRate(CartCheckoutSpan));

            VerifySingleRule(cartRule, CartCheckoutSpan, true);
            VerifySingleRule(cartRule, AddToCartSpan, true);
            VerifySingleRule(cartRule, ShippingAuthSpan, false);
            VerifySingleRule(cartRule, ShippingRevertSpan, false);
            VerifySingleRule(cartRule, RequestShippingSpan, false);

            var shippingAuthRule = rules[1];
            Assert.Equal(expected: 1f, actual: shippingAuthRule.GetSamplingRate(CartCheckoutSpan));

            VerifySingleRule(shippingAuthRule, CartCheckoutSpan, false);
            VerifySingleRule(shippingAuthRule, AddToCartSpan, false);
            VerifySingleRule(shippingAuthRule, ShippingAuthSpan, true);
            VerifySingleRule(shippingAuthRule, ShippingRevertSpan, false);
            VerifySingleRule(shippingAuthRule, RequestShippingSpan, false);

            var fallbackShippingRule = rules[2];
            Assert.Equal(expected: 0.1f, actual: fallbackShippingRule.GetSamplingRate(CartCheckoutSpan));

            VerifySingleRule(fallbackShippingRule, CartCheckoutSpan, false);
            VerifySingleRule(fallbackShippingRule, AddToCartSpan, false);
            VerifySingleRule(fallbackShippingRule, ShippingAuthSpan, true);
            VerifySingleRule(fallbackShippingRule, ShippingRevertSpan, true);
            VerifySingleRule(fallbackShippingRule, RequestShippingSpan, true);

            var fallbackRule = rules[3];
            Assert.Equal(expected: 0.05f, actual: fallbackRule.GetSamplingRate(CartCheckoutSpan));

            VerifySingleRule(fallbackRule, CartCheckoutSpan, true);
            VerifySingleRule(fallbackRule, AddToCartSpan, true);
            VerifySingleRule(fallbackRule, ShippingAuthSpan, true);
            VerifySingleRule(fallbackRule, ShippingRevertSpan, true);
            VerifySingleRule(fallbackRule, RequestShippingSpan, true);
        }

        [Theory]
        [InlineData("\"rate:0.5, \"name\":\"auth.*\"}]")]
        [InlineData("[{\"name\":\"wat\"}]")]
        public void Malformed_Rules_Do_Not_Register_Or_Crash(string ruleConfig)
        {
            var rules = CustomSamplingRule.BuildFromConfigurationString(ruleConfig).ToArray();
            Assert.Empty(rules);
        }

        private void VerifyRate(string config, float expectedRate)
        {
            var rule = CustomSamplingRule.BuildFromConfigurationString(config).Single();
            Assert.Equal(expected: expectedRate, actual: rule.GetSamplingRate(CartCheckoutSpan));
        }

        private void VerifySingleRule(string config, Span span, bool isMatch)
        {
            var rule = CustomSamplingRule.BuildFromConfigurationString(config).Single();
            VerifySingleRule(rule, span, isMatch);
        }

        private void VerifySingleRule(ISamplingRule rule, Span span, bool isMatch)
        {
            Assert.Equal(rule.IsMatch(span), isMatch);
        }
    }
}
