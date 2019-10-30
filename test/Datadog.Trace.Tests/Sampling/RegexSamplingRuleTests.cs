using System;
using System.Linq;
using Datadog.Trace.Sampling;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class RegexSamplingRuleTests
    {
        private static readonly ulong _id = 1;
        private static readonly Span CartCheckoutSpan = new Span(new SpanContext(_id++, _id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "checkout" };
        private static readonly Span AddToCartSpan = new Span(new SpanContext(_id++, _id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "cart-add" };
        private static readonly Span ShippingAuthSpan = new Span(new SpanContext(_id++, _id++, null, serviceName: "shipping-auth-service"), DateTimeOffset.Now) { OperationName = "authorize" };
        private static readonly Span ShippingRevertSpan = new Span(new SpanContext(_id++, _id++, null, serviceName: "shipping-auth-service"), DateTimeOffset.Now) { OperationName = "authorize-revert" };
        private static readonly Span RequestShippingSpan = new Span(new SpanContext(_id++, _id++, null, serviceName: "request-shipping"), DateTimeOffset.Now) { OperationName = "submit" };

        [Fact]
        public void Constructs_ZeroRateOnly_From_Config_String()
        {
            var config = "rate=0";
            var rule = RegexSamplingRule.BuildFromConfigurationString(config).Single();
            Assert.Equal(expected: 0, actual: rule.GetSamplingRate());

            Assert.True(rule.IsMatch(CartCheckoutSpan));
            Assert.True(rule.IsMatch(AddToCartSpan));
            Assert.True(rule.IsMatch(ShippingAuthSpan));
            Assert.True(rule.IsMatch(ShippingRevertSpan));
            Assert.True(rule.IsMatch(RequestShippingSpan));
        }

        [Fact]
        public void Constructs_CartOnlyRule_From_Config_String()
        {
            var config = "rate=0.5, service=shopping-cart.*";
            var rule = RegexSamplingRule.BuildFromConfigurationString(config).Single();

            Assert.True(rule.IsMatch(CartCheckoutSpan));
            Assert.True(rule.IsMatch(AddToCartSpan));
            Assert.False(rule.IsMatch(ShippingAuthSpan));
            Assert.False(rule.IsMatch(ShippingRevertSpan));
            Assert.False(rule.IsMatch(RequestShippingSpan));
        }

        [Fact]
        public void Constructs_All_Expected_From_Config_String()
        {
            var config = "rate=0.5, service=.*cart.*;  rate=1, service=.*shipping.*, operation=authorize; rate=0.1, service=.*shipping.*; rate=0.05";
            var rules = RegexSamplingRule.BuildFromConfigurationString(config).ToArray();

            var cartRule = rules[0];
            Assert.Equal(expected: 0.5f, actual: cartRule.GetSamplingRate());

            Assert.True(cartRule.IsMatch(CartCheckoutSpan));
            Assert.True(cartRule.IsMatch(AddToCartSpan));
            Assert.False(cartRule.IsMatch(ShippingAuthSpan));
            Assert.False(cartRule.IsMatch(ShippingRevertSpan));
            Assert.False(cartRule.IsMatch(RequestShippingSpan));

            var shippingAuthRule = rules[1];
            Assert.Equal(expected: 1f, actual: shippingAuthRule.GetSamplingRate());

            Assert.False(shippingAuthRule.IsMatch(CartCheckoutSpan));
            Assert.False(shippingAuthRule.IsMatch(AddToCartSpan));
            Assert.True(shippingAuthRule.IsMatch(ShippingAuthSpan));
            Assert.False(shippingAuthRule.IsMatch(ShippingRevertSpan));
            Assert.False(shippingAuthRule.IsMatch(RequestShippingSpan));

            var fallbackShippingRule = rules[2];
            Assert.Equal(expected: 0.1f, actual: fallbackShippingRule.GetSamplingRate());

            Assert.False(fallbackShippingRule.IsMatch(CartCheckoutSpan));
            Assert.False(fallbackShippingRule.IsMatch(AddToCartSpan));
            Assert.True(fallbackShippingRule.IsMatch(ShippingAuthSpan));
            Assert.True(fallbackShippingRule.IsMatch(ShippingRevertSpan));
            Assert.True(fallbackShippingRule.IsMatch(RequestShippingSpan));

            var fallbackRule = rules[3];
            Assert.Equal(expected: 0.05f, actual: fallbackRule.GetSamplingRate());

            Assert.True(fallbackRule.IsMatch(CartCheckoutSpan));
            Assert.True(fallbackRule.IsMatch(AddToCartSpan));
            Assert.True(fallbackRule.IsMatch(ShippingAuthSpan));
            Assert.True(fallbackRule.IsMatch(ShippingRevertSpan));
            Assert.True(fallbackRule.IsMatch(RequestShippingSpan));
        }
    }
}
