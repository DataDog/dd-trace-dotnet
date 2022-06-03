// <copyright file="DefaultSamplingRuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    public class DefaultSamplingRuleTests
    {
        [Theory]
        // Value returned by the agent per default
        [InlineData("service:,env:", "hello", "world", 1f)]
        // Does not match
        [InlineData("service:nope,env:nope", "hello", "world", 1f)]
        // Nominal case
        [InlineData("service:hello,env:world", "hello", "world", .5f)]
        // Too many values
        [InlineData("service:hello,env:world,xxxx", "hello", "world", 1f)]
        // ':' in service name
        [InlineData("service:hello:1,env:world", "hello:1", "world", .5f)]
        // ':' in env name
        [InlineData("service:hello,env:world:1", "hello", "world:1", .5f)]
        public void KeyParsing(string key, string expectedService, string expectedEnv, float expectedRate)
        {
            var rule = new DefaultSamplingRule();

            rule.SetDefaultSampleRates(new Dictionary<string, float> { { key, .5f } });

            var span = new Span(new SpanContext(1, 1, null, serviceName: expectedService), DateTimeOffset.Now);
            span.SetTag(Tags.Env, expectedEnv);

            var samplingRate = rule.GetSamplingRate(span);

            Assert.Equal(expectedRate, samplingRate);
        }

        [Fact]
        public void DefaultSamplingRuleIsApplied()
        {
            const string configuredService = "NiceService";
            const string configuredEnv = "BeautifulEnv";
            const string unconfiguredService = "RogueService";

            var rule = new DefaultSamplingRule();
            var span = new Span(new SpanContext(1, 1, null, serviceName: configuredService), DateTimeOffset.Now);
            var secondSpan = new Span(new SpanContext(2, 2, null, serviceName: unconfiguredService), DateTimeOffset.Now);
            span.SetTag(Tags.Env, configuredEnv);
            secondSpan.SetTag(Tags.Env, configuredEnv);

            rule.GetSamplingRate(span).Should().Be(1f); // as we haven't configured it yet.

            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:,env:", .5f },
                { $"service:{configuredService},env:{configuredEnv}", .1f }
            });

            rule.GetSamplingRate(span).Should().Be(.1f);
            rule.GetSamplingRate(secondSpan).Should().Be(.5f); // as it should use the new default sampling.
        }
    }
}
