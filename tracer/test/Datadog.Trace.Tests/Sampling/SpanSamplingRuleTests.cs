// <copyright file="SpanSamplingRuleTests.cs" company="Datadog">
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
    public class SpanSamplingRuleTests
    {
        // copied these from CustomSamplingRule - maybe should combine or share?
        private static readonly ulong Id = 1;
        private static readonly Span CartCheckoutSpan;
        private static readonly Span AddToCartSpan;
        private static readonly Span ShippingAuthSpan;
        private static readonly Span ShippingRevertSpan;
        private static readonly Span RequestShippingSpan;

        static SpanSamplingRuleTests()
        {
            CartCheckoutSpan = Span.CreateSpan(Span.CreateSpanContext(Id++, Id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now);
            CartCheckoutSpan.OperationName = "checkout";

            AddToCartSpan = Span.CreateSpan(Span.CreateSpanContext(Id++, Id++, null, serviceName: "shopping-cart-service"), DateTimeOffset.Now);
            AddToCartSpan.OperationName = "cart-add";

            ShippingAuthSpan = Span.CreateSpan(Span.CreateSpanContext(Id++, Id++, null, serviceName: "shipping-auth-service"), DateTimeOffset.Now);
            ShippingAuthSpan.OperationName = "authorize";

            ShippingRevertSpan = Span.CreateSpan(Span.CreateSpanContext(Id++, Id++, null, serviceName: "shipping-auth-service"), DateTimeOffset.Now);
            ShippingRevertSpan.OperationName = "authorize-revert";

            RequestShippingSpan = Span.CreateSpan(Span.CreateSpanContext(Id++, Id++, null, serviceName: "request-shipping"), DateTimeOffset.Now);
            RequestShippingSpan.OperationName = "submit";
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")] // empty
        [InlineData(" ")] // whitespace
        public void Constructor_ShouldThrow_ArgumentException_WhenServiceNameGlob_EmptyOrNull(string serviceNameGlob)
        {
            var ctor = () => new SpanSamplingRule(serviceNameGlob, "*", 1.0f, null);
            ctor.Should().Throw<ArgumentException>().WithParameterName("serviceNameGlob");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")] // empty
        [InlineData(" ")] // whitespace
        public void Constructor_ShouldThrow_ArgumentException_WhenOperationNameGlob_EmptyOrNull(string operationNameGlob)
        {
            var ctor = () => new SpanSamplingRule("*", operationNameGlob, 1.0f, null);
            ctor.Should().Throw<ArgumentException>().WithParameterName("operationNameGlob");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void BuildFromConfigurationString_ShouldReturnEmpty_WhenNoConfigGiven(string config)
        {
            SpanSamplingRule.BuildFromConfigurationString(config).Should().BeEmpty();
        }

        [Theory]
        [InlineData("[{\"service\":\"shopping-cart*\", \"name\":\"checkou?\", \"sample_rate\":0.5}]")]
        [InlineData("[{\"service\":\"shopping-cart*\", \"name\":\"checkou?\", \"max_per_second\":1000.5}]")]
        public void BuildFromConfigurationString_ShouldHandle_MissingOptionals(string config)
        {
            SpanSamplingRule.BuildFromConfigurationString(config).Should().ContainSingle();
        }

        [Theory]
        [InlineData("test")]
        public void BuildFromConfigurationString_ShouldHandle_MalformedData(string config)
        {
            SpanSamplingRule.BuildFromConfigurationString(config).Should().BeEmpty();
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
            rule.IsMatch(CartCheckoutSpan).Should().Be(shouldMatch);
            rule.IsMatch(AddToCartSpan).Should().Be(shouldMatch);
            rule.IsMatch(ShippingAuthSpan).Should().Be(shouldMatch);
            rule.IsMatch(ShippingRevertSpan).Should().Be(shouldMatch);
            rule.IsMatch(RequestShippingSpan).Should().Be(shouldMatch);
        }

        [Fact]
        public void IsMatch_ShouldReturnFalse_ForNullSpan()
        {
            var rule = new SpanSamplingRule("*", "*");

            rule.IsMatch(null).Should().BeFalse();
        }

        [Fact]
        public void ShouldSample_ShouldReturnFalse_ForNullSpan()
        {
            var rule = new SpanSamplingRule("*", "*");

            rule.ShouldSample(null).Should().BeFalse();
        }

        [Fact]
        public void IsMatch_ShouldReturnFalse_WhenServiceAndOperationDontMatch()
        {
            var config = "[{\"service\":\"test\", \"name\":\"test\"}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            rule.IsMatch(CartCheckoutSpan).Should().BeFalse();
        }

        [Fact]
        public void ShouldSample_ShouldReturnFalse_WhenSamplerIsZero()
        {
            var config = "[{\"service\":\"*\", \"name\":\"*\", \"sample_rate\":0.0}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            rule.ShouldSample(CartCheckoutSpan).Should().BeFalse();
        }

        [Fact]
        public void ShouldSample_ShouldReturnTrue_WhenEverythingMatches()
        {
            var config = "[{\"service\":\"*\", \"name\":\"*\"}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            rule.IsMatch(CartCheckoutSpan).Should().BeTrue();
            rule.ShouldSample(CartCheckoutSpan).Should().BeTrue();
        }

        [Fact]
        public void MaxPerSecond_ShouldDefaultTo_NullWhenAbsent()
        {
            var config = "[{\"service\":\"*\", \"name\":\"*\"}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            rule.MaxPerSecond.Should().BeNull();
        }

        [Fact]
        public void SampleRate_ShouldDefaultTo_OneWhenAbsent()
        {
            var config = "[{\"service\":\"*\", \"name\":\"*\"}]";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();

            rule.SamplingRate.Should().Be(1.0f);
        }

        private void VerifySingleRule(string config, Span span, bool isMatch)
        {
            var rule = SpanSamplingRule.BuildFromConfigurationString(config).Single();
            VerifySingleRule(rule, span, isMatch);
        }

        private void VerifySingleRule(SpanSamplingRule rule, Span span, bool isMatch)
        {
            rule.IsMatch(span).Should().Be(isMatch);
        }
    }
}
