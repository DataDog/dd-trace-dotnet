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
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Theory]
        [InlineData(null)]
        [InlineData("")] // empty
        [InlineData(" ")] // whitespace
        public void Constructor_ShouldThrow_ArgumentException_WhenServiceNameGlob_EmptyOrNull(string serviceNameGlob)
        {
            var ctor = () => new SpanSamplingRule(
                serviceNameGlob: serviceNameGlob,
                operationNameGlob: "*",
                resourceNameGlob: null,
                tagGlobs: null,
                timeout: Timeout,
                samplingRate: 1.0f,
                maxPerSecond: null);

            ctor.Should().Throw<ArgumentException>().WithParameterName("serviceNameGlob");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")] // empty
        [InlineData(" ")] // whitespace
        public void Constructor_ShouldThrow_ArgumentException_WhenOperationNameGlob_EmptyOrNull(string operationNameGlob)
        {
            var ctor = () => new SpanSamplingRule(
                "*",
                operationNameGlob,
                resourceNameGlob: null,
                tagGlobs: null,
                timeout: Timeout,
                samplingRate: 1.0f,
                maxPerSecond: null);

            ctor.Should().Throw<ArgumentException>().WithParameterName("operationNameGlob");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void BuildFromConfigurationString_ShouldReturnEmpty_WhenNoConfigGiven(string config)
        {
            SpanSamplingRule.BuildFromConfigurationString(config, Timeout).Should().BeEmpty();
        }

        [Theory]
        [InlineData("""[{"service":"shopping-cart*", "name":"checkou?", "sample_rate":0.5}]""")]
        [InlineData("""[{"service":"shopping-cart*", "name":"checkou?", "max_per_second":1000.5}]""")]
        public void BuildFromConfigurationString_ShouldHandle_MissingOptionals(string config)
        {
            SpanSamplingRule.BuildFromConfigurationString(config, Timeout).Should().ContainSingle();
        }

        [Theory]
        [InlineData("test")]
        public void BuildFromConfigurationString_ShouldHandle_MalformedData(string config)
        {
            SpanSamplingRule.BuildFromConfigurationString(config, Timeout).Should().BeEmpty();
        }

        [Fact]
        public void BuildFromConfigurationString_Should_ReturnSingleRule()
        {
            var config = """[{"service":"shopping-cart*", "name":"checkou?", "sample_rate":0.5, "max_per_second":1000.5}]""";

            VerifySingleRule(config, TestSpans.CartCheckoutSpan, true);
            VerifySingleRule(config, TestSpans.AddToCartSpan, false);
            VerifySingleRule(config, TestSpans.ShippingAuthSpan, false);
            VerifySingleRule(config, TestSpans.ShippingRevertSpan, false);
            VerifySingleRule(config, TestSpans.RequestShippingSpan, false);
        }

        [Fact]
        public void WildcardService_ShouldMatch_OnOperation()
        {
            var config = """[{"service":"*", "name":"authorize", "sample_rate":0.5, "max_per_second":1000.5}]""";

            VerifySingleRule(config, TestSpans.CartCheckoutSpan, false);
            VerifySingleRule(config, TestSpans.AddToCartSpan, false);
            VerifySingleRule(config, TestSpans.ShippingAuthSpan, true);
            VerifySingleRule(config, TestSpans.ShippingRevertSpan, false);
            VerifySingleRule(config, TestSpans.RequestShippingSpan, false);
        }

        [Fact]
        public void WildcardOperation_ShouldMatch_OnService()
        {
            var config = """[{"service":"shopping-cart-service", "name":"*", "sample_rate":0.5, "max_per_second":1000.5}]""";

            VerifySingleRule(config, TestSpans.CartCheckoutSpan, true);
            VerifySingleRule(config, TestSpans.AddToCartSpan, true);
            VerifySingleRule(config, TestSpans.ShippingAuthSpan, false);
            VerifySingleRule(config, TestSpans.ShippingRevertSpan, false);
            VerifySingleRule(config, TestSpans.RequestShippingSpan, false);
        }

        [Fact]
        public void WildcardOperation_ShouldMatch_OnResource()
        {
            var config = """[{"service":"*", "name":"*", "resource": "/api/users/*", "sample_rate":0.5, "max_per_second":1000.5}]""";

            VerifySingleRule(config, TestSpans.CartCheckoutSpan, true);
            VerifySingleRule(config, TestSpans.AddToCartSpan, false);
            VerifySingleRule(config, TestSpans.ShippingAuthSpan, false);
            VerifySingleRule(config, TestSpans.ShippingRevertSpan, true);
            VerifySingleRule(config, TestSpans.RequestShippingSpan, true);
        }

        [Fact]
        public void WildcardOperation_ShouldMatch_OnTags()
        {
            var config = """
                         [{
                            "service":"*",
                            "name":"*",
                            "tags": {
                                "tag1": "value*",
                                "tag2": "40?",
                            },
                            "sample_rate":0.5,
                            "max_per_second":1000.5
                         }]
                         """;

            VerifySingleRule(config, TestSpans.CartCheckoutSpan, true);
            VerifySingleRule(config, TestSpans.AddToCartSpan, true);
            VerifySingleRule(config, TestSpans.ShippingAuthSpan, false);
            VerifySingleRule(config, TestSpans.ShippingRevertSpan, false);
            VerifySingleRule(config, TestSpans.RequestShippingSpan, false);
        }

        [Theory]
        [InlineData("*", "*", "*")]
        public void MatchAll_ShouldMatchAll(string serviceGlob, string operationGlob, string resourceGlob)
        {
            var rule = new SpanSamplingRule(
                serviceNameGlob: serviceGlob,
                operationNameGlob: operationGlob,
                resourceNameGlob: resourceGlob,
                tagGlobs: null,
                timeout: Timeout);

            rule.IsMatch(TestSpans.CartCheckoutSpan).Should().Be(true);
            rule.IsMatch(TestSpans.AddToCartSpan).Should().Be(true);
            rule.IsMatch(TestSpans.ShippingAuthSpan).Should().Be(true);
            rule.IsMatch(TestSpans.ShippingRevertSpan).Should().Be(true);
            rule.IsMatch(TestSpans.RequestShippingSpan).Should().Be(true);
        }

        [Fact]
        public void IsMatch_ShouldReturnFalse_ForNullSpan()
        {
            var rule = new SpanSamplingRule(
                serviceNameGlob: "*",
                operationNameGlob: "*",
                resourceNameGlob: "*",
                tagGlobs: null,
                timeout: Timeout);

            rule.IsMatch(null).Should().BeFalse();
        }

        [Fact]
        public void ShouldSample_ShouldReturnFalse_ForNullSpan()
        {
            var rule = new SpanSamplingRule(
                serviceNameGlob: "*",
                operationNameGlob: "*",
                resourceNameGlob: "*",
                tagGlobs: null,
                timeout: Timeout);

            rule.ShouldSample(null).Should().BeFalse();
        }

        [Fact]
        public void IsMatch_ShouldReturnFalse_WhenServiceAndOperationDontMatch()
        {
            var config = """[{"service":"test", "name":"test"}]""";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config, Timeout).Single();

            rule.IsMatch(TestSpans.CartCheckoutSpan).Should().BeFalse();
        }

        [Fact]
        public void ShouldSample_ShouldReturnFalse_WhenSamplerIsZero()
        {
            var config = """[{"service":"*", "name":"*", "sample_rate":0.0}]""";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config, Timeout).Single();

            rule.ShouldSample(TestSpans.CartCheckoutSpan).Should().BeFalse();
        }

        [Fact]
        public void ShouldSample_ShouldReturnTrue_WhenEverythingMatches()
        {
            var config = """[{"service":"*", "name":"*"}]""";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config, Timeout).Single();

            rule.IsMatch(TestSpans.CartCheckoutSpan).Should().BeTrue();
            rule.ShouldSample(TestSpans.CartCheckoutSpan).Should().BeTrue();
        }

        [Fact]
        public void MaxPerSecond_ShouldDefaultTo_NullWhenAbsent()
        {
            var config = """[{"service":"*", "name":"*"}]""";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config, Timeout).Single();

            rule.MaxPerSecond.Should().BeNull();
        }

        [Fact]
        public void SampleRate_ShouldDefaultTo_OneWhenAbsent()
        {
            var config = """[{"service":"*", "name":"*"}]""";
            var rule = SpanSamplingRule.BuildFromConfigurationString(config, Timeout).Single();

            rule.SamplingRate.Should().Be(1.0f);
        }

        private void VerifySingleRule(string config, Span span, bool isMatch)
        {
            var rule = SpanSamplingRule.BuildFromConfigurationString(config, Timeout).Single();
            VerifySingleRule(rule, span, isMatch);
        }

        private void VerifySingleRule(SpanSamplingRule rule, Span span, bool isMatch)
        {
            rule.IsMatch(span).Should().Be(isMatch);
        }
    }
}
