// <copyright file="DefaultSamplingRuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    public class DefaultSamplingRuleTests
    {
        [Theory]
        // Value returned by the agent per default
        [InlineData("service:,env:", "hello", "world", 0.5f)]
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
            // create span, setting service and environment
            var settings = new TracerSettings { ServiceName = expectedService };
            var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);
            using var scope = (Scope)tracer.StartActive("root");
            scope.Span.TraceContext.Environment = expectedEnv;

            // create sampling rule
            var rule = new DefaultSamplingRule();
            rule.SetDefaultSampleRates(new Dictionary<string, float> { { key, .5f } });

            // assert that the sampling rate applied to the span is correct
            rule.GetSamplingRate(scope.Span).Should().Be(expectedRate);
        }

        [Fact]
        public void DefaultSamplingRuleIsApplied()
        {
            const string configuredService = "NiceService";
            const string configuredEnv = "BeautifulEnv";
            const string unconfiguredService = "RogueService";

            var rule = new DefaultSamplingRule();

            var settings = new TracerSettings();
            var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);

            var firstScope = (Scope)tracer.StartActive("first");
            var firstSpan = firstScope.Span;
            firstSpan.ServiceName = configuredService;
            firstSpan.TraceContext.Environment = configuredEnv;

            var secondScope = (Scope)tracer.StartActive("second");
            var secondSpan = secondScope.Span;
            secondSpan.ServiceName = unconfiguredService;

            rule.GetSamplingRate(firstSpan).Should().Be(1f); // as we haven't configured it yet.

            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:,env:", .5f },
                { $"service:{configuredService},env:{configuredEnv}", .1f }
            });

            rule.GetSamplingRate(firstSpan).Should().Be(.1f);
            rule.GetSamplingRate(secondSpan).Should().Be(.5f); // as it should use the new default sampling.
        }
    }
}
