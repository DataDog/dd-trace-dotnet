// <copyright file="SpanSamplingRuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Sampling;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class SpanSamplingRuleTests
    {
        // copied these from CustomSamplingRule - maybe should combine or share?
        private static readonly ulong Id = 1;
        private static readonly Span CartCheckoutSpan = new Span(new SpanContext(Id++, Id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "checkout" };
        private static readonly Span AddToCartSpan = new Span(new SpanContext(Id++, Id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "cart-add" };
        private static readonly Span ShippingAuthSpan = new Span(new SpanContext(Id++, Id++, null, serviceName: "shipping-auth-service"), DateTimeOffset.Now) { OperationName = "authorize" };
        private static readonly Span ShippingRevertSpan = new Span(new SpanContext(Id++, Id++, null, serviceName: "shipping-auth-service"), DateTimeOffset.Now) { OperationName = "authorize-revert" };
        private static readonly Span RequestShippingSpan = new Span(new SpanContext(Id++, Id++, null, serviceName: "request-shipping"), DateTimeOffset.Now) { OperationName = "submit" };

        [Theory]
        [InlineData(null)]
        [InlineData("")] // empty
        [InlineData(" ")] // whitespace
        public void Constructor_ShouldThrow_ArgumentException_WhenServiceNameGlob_EmptyOrNull(string serviceNameGlob)
        {
            var exception = Assert.Throws<ArgumentException>(() => new SpanSamplingRule(serviceNameGlob, "*", 1.0f, null));
            Assert.Equal("serviceNameGlob", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")] // empty
        [InlineData(" ")] // whitespace
        public void Constructor_ShouldThrow_ArgumentException_WhenOperationNameGlob_EmptyOrNull(string operationNameGlob)
        {
            var exception = Assert.Throws<ArgumentException>(() => new SpanSamplingRule("*", operationNameGlob, 1.0f, null));
            Assert.Equal("operationNameGlob", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void BuildFromConfigurationString_ShouldReturnEmpty_WhenNoConfigGiven(string config)
        {
            var result = SpanSamplingRule.BuildFromConfigurationString(config);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData("[{\"service\":\"shopping-cart*\", \"name\":\"checkou?\", \"sample_rate\":0.5}]")]
        [InlineData("[{\"service\":\"shopping-cart*\", \"name\":\"checkou?\", \"max_per_second\":1000.5}]")]
        public void BuildFromConfigurationString_ShouldHandle_MissingOptionals(string config)
        {
            var result = SpanSamplingRule.BuildFromConfigurationString(config);

            Assert.Single(result);
        }

        [Theory]
        [InlineData("test")]
        public void BuildFromConfigurationString_ShouldHandle_MalformedData(string config)
        {
            var result = SpanSamplingRule.BuildFromConfigurationString(config);
            Assert.Empty(result);
        }

        [Fact]
        public void BuildFromConfigurationString_Should_ReturnSingleRule()
        {
            var config = "[{\"service\":\"shopping-cart*\", \"name\":\"checkou?\", \"sample_rate\":0.5, \"max_per_second\":1000.5}]";

            VerifySingleRule(config, CartCheckoutSpan, true);
            VerifySingleRule(config, AddToCartSpan, false);
            VerifySingleRule(config, ShippingAuthSpan, false);
            VerifySingleRule(config, ShippingRevertSpan, false);
            VerifySingleRule(config, RequestShippingSpan, false);
        }

        [Fact]
        public void WildcardService_ShouldMatch_OnOperation()
        {
            var config = "[{\"service\":\"*\", \"name\":\"authorize\", \"sample_rate\":0.5, \"max_per_second\":1000.5}]";

            VerifySingleRule(config, CartCheckoutSpan, false);
            VerifySingleRule(config, AddToCartSpan, false);
            VerifySingleRule(config, ShippingAuthSpan, true);
            VerifySingleRule(config, ShippingRevertSpan, false);
            VerifySingleRule(config, RequestShippingSpan, false);
        }

        [Fact]
        public void WildcardOperation_ShouldMatch_OnService()
        {
            var config = "[{\"service\":\"shopping-cart-service\", \"name\":\"*\", \"sample_rate\":0.5, \"max_per_second\":1000.5}]";

            VerifySingleRule(config, CartCheckoutSpan, true);
            VerifySingleRule(config, AddToCartSpan, true);
            VerifySingleRule(config, ShippingAuthSpan, false);
            VerifySingleRule(config, ShippingRevertSpan, false);
            VerifySingleRule(config, RequestShippingSpan, false);
        }

        [Theory]
        [InlineData("*", "*", true)]
        public void MatchAll_ShouldMatchAll(string serviceGlob, string operationGlob, bool shouldMatch)
        {
            var rule = new SpanSamplingRule(serviceGlob, operationGlob);
            Assert.Equal(rule.IsMatch(CartCheckoutSpan), shouldMatch);
            Assert.Equal(rule.IsMatch(AddToCartSpan), shouldMatch);
            Assert.Equal(rule.IsMatch(ShippingAuthSpan), shouldMatch);
            Assert.Equal(rule.IsMatch(ShippingRevertSpan), shouldMatch);
            Assert.Equal(rule.IsMatch(RequestShippingSpan), shouldMatch);
        }

        [Fact]
        public void IsMatch_ShouldReturnFalse_ForNullSpan()
        {
            var rule = new SpanSamplingRule("*", "*");

            Assert.False(rule.IsMatch(null));
        }

        [Fact]
        public void ShouldSample_ShouldReturnFalse_ForNullSpan()
        {
            var rule = new SpanSamplingRule("*", "*");

            Assert.False(rule.ShouldSample(null));
        }

        [Fact]
        public void IsMatch_ShouldReturnFalse_WhenServiceAndOperationDontMatch()
        {
            var config = "[{\"service\":\"test\", \"name\":\"test\"}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            Assert.False(rule.IsMatch(CartCheckoutSpan));
        }

        [Fact]
        public void ShouldSample_ShouldReturnFalse_WhenSamplerIsZero()
        {
            var config = "[{\"service\":\"*\", \"name\":\"*\", \"sample_rate\":0.0}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            Assert.False(rule.ShouldSample(CartCheckoutSpan));
        }

        [Fact]
        public void ShouldSample_ShouldReturnTrue_WhenEverythingMatches()
        {
            var config = "[{\"service\":\"*\", \"name\":\"*\"}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            Assert.True(rule.IsMatch(CartCheckoutSpan));
            Assert.True(rule.ShouldSample(CartCheckoutSpan));
        }

        [Fact]
        public void MaxPerSecond_ShouldDefaultTo_NullWhenAbsent()
        {
            var config = "[{\"service\":\"*\", \"name\":\"*\"}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            Assert.Null(rule.MaxPerSecond);
        }

        [Fact]
        public void SampleRate_ShouldDefaultTo_OneWhenAbsent()
        {
            var config = "[{\"service\":\"*\", \"name\":\"*\"}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            Assert.Equal(1.0f, rule.SamplingRate);
        }

        private void VerifySingleRule(string config, Span span, bool isMatch)
        {
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();
            VerifySingleRule(rule, span, isMatch);
        }

        private void VerifySingleRule(SpanSamplingRule rule, Span span, bool isMatch)
        {
            Assert.Equal(rule.IsMatch(span), isMatch);
        }

        // TODO no tests yet for rate limit / sampling priority
    }
}
