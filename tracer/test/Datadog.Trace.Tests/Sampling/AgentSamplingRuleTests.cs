// <copyright file="AgentSamplingRuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    public class AgentSamplingRuleTests
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
        public async Task KeyParsing(string key, string expectedService, string expectedEnv, float expectedRate)
        {
            // create span, setting service and environment
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.ServiceName, expectedService } });
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            using var scope = (Scope)tracer.StartActive("root");
            scope.Span.Context.TraceContext.Environment = expectedEnv;

            // create sampling rule
            var rule = new AgentSamplingRule();
            rule.SetDefaultSampleRates(new Dictionary<string, float> { { key, .5f } });

            // assert that the sampling rate applied to the span is correct
            rule.GetSamplingRate(scope.Span).Should().Be(expectedRate);
        }

        [Fact]
        public async Task SamplingRulesAreApplied()
        {
            const string configuredService = "NiceService";
            const string configuredEnv = "BeautifulEnv";
            const string unconfiguredService = "RogueService";

            var rule = new AgentSamplingRule();

            var settings = new TracerSettings();
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);

            var firstScope = (Scope)tracer.StartActive("first");
            var firstSpan = firstScope.Span;
            firstSpan.ServiceName = configuredService;
            firstSpan.Context.TraceContext.Environment = configuredEnv;

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

        [Theory]
        [InlineData(0.8f, 0.4f, true, 0.4f)]  // decrease applied immediately
        [InlineData(0.8f, 0.4f, false, 0.4f)] // decrease applied even during cooldown
        [InlineData(0.5f, 0.5f, true, 0.5f)]  // no change
        [InlineData(0.0f, 0.5f, true, 0.5f)]  // transition from zero applied immediately
        [InlineData(0.0f, 0.5f, false, 0.5f)] // transition from zero applied even during cooldown
        [InlineData(0.1f, 1.0f, true, 0.2f)]  // increase capped at 2x
        [InlineData(0.2f, 1.0f, true, 0.4f)]  // increase capped at 2x
        [InlineData(0.4f, 1.0f, true, 0.8f)]  // increase capped at 2x
        [InlineData(0.4f, 0.5f, true, 0.5f)]  // increase where 2x exceeds target uses target
        [InlineData(0.1f, 1.0f, false, 0.1f)] // increase blocked during cooldown
        [InlineData(0.2f, 0.8f, false, 0.2f)] // increase blocked during cooldown
        public void CappedRate(float oldRate, float newRate, bool canIncrease, float expected)
        {
            AgentSamplingRule.CappedRate(oldRate, newRate, canIncrease).Should().Be(expected);
        }

        [Fact]
        public async Task RampUpCapsRateIncreasesAt2xPerInterval()
        {
            var clock = new SimpleClock();
            using var lease = Clock.SetForCurrentThread(clock);

            var rule = new AgentSamplingRule();

            var settings = new TracerSettings();
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);

            var scope = (Scope)tracer.StartActive("test");
            var span = scope.Span;
            span.ServiceName = "web";
            span.Context.TraceContext.Environment = "prod";

            // Set initial low rate (decrease from default 1.0, applied immediately)
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 0.1f },
                { "service:,env:", 0.1f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.1f, 0.01f);

            // Simulate agent restart: rate jumps to 1.0
            // First update after ramp interval: capped at 0.1 * 2 = 0.2
            clock.UtcNow = clock.UtcNow.AddSeconds(1.1);
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 1.0f },
                { "service:,env:", 1.0f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.2f, 0.01f);

            // Second update: capped at 0.2 * 2 = 0.4
            clock.UtcNow = clock.UtcNow.AddSeconds(1.1);
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 1.0f },
                { "service:,env:", 1.0f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.4f, 0.01f);

            // Third: 0.8
            clock.UtcNow = clock.UtcNow.AddSeconds(1.1);
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 1.0f },
                { "service:,env:", 1.0f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.8f, 0.01f);

            // Fourth: reaches target 1.0 (2x=1.6 > 1.0)
            clock.UtcNow = clock.UtcNow.AddSeconds(1.1);
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 1.0f },
                { "service:,env:", 1.0f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(1.0f, 0.01f);
        }

        [Fact]
        public async Task RampDownAppliesImmediately()
        {
            var clock = new SimpleClock();
            using var lease = Clock.SetForCurrentThread(clock);

            var rule = new AgentSamplingRule();

            var settings = new TracerSettings();
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);

            var scope = (Scope)tracer.StartActive("test");
            var span = scope.Span;
            span.ServiceName = "web";
            span.Context.TraceContext.Environment = "prod";

            // Set initial rate to 0.8 (decrease from default 1.0)
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 0.8f },
            });

            // Decrease to 0.2: applied immediately
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 0.2f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.2f, 0.01f);
        }

        [Fact]
        public async Task RateIncreaseBlockedDuringCooldown()
        {
            var clock = new SimpleClock();
            using var lease = Clock.SetForCurrentThread(clock);

            var rule = new AgentSamplingRule();

            var settings = new TracerSettings();
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);

            var scope = (Scope)tracer.StartActive("test");
            var span = scope.Span;
            span.ServiceName = "web";
            span.Context.TraceContext.Environment = "prod";

            // Set initial rate to 0.1
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 0.1f },
            });

            // First capped increase after interval
            clock.UtcNow = clock.UtcNow.AddSeconds(1.1);
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 1.0f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.2f, 0.01f);

            // Try again after 0.5s (within cooldown) — rate stays at 0.2
            clock.UtcNow = clock.UtcNow.AddSeconds(0.5);
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 1.0f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.2f, 0.01f);          

            // Try again after 0.6s (puts total since change above cooldown) — rate doubles to 0.4
            clock.UtcNow = clock.UtcNow.AddSeconds(0.6);
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:web,env:prod", 1.0f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.4f, 0.01f);
        }

        [Fact]
        public async Task DefaultRateAlsoCapped()
        {
            var clock = new SimpleClock();
            using var lease = Clock.SetForCurrentThread(clock);

            var rule = new AgentSamplingRule();

            var settings = new TracerSettings();
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);

            var scope = (Scope)tracer.StartActive("test");
            var span = scope.Span;
            span.ServiceName = "unknown-service";
            span.Context.TraceContext.Environment = string.Empty;

            // Set default rate to 0.1 (decrease from initial 1.0)
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:,env:", 0.1f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.1f, 0.01f);

            // Increase default to 1.0: capped at 0.1 * 2 = 0.2
            clock.UtcNow = clock.UtcNow.AddSeconds(1.1);
            rule.SetDefaultSampleRates(new Dictionary<string, float>
            {
                { "service:,env:", 1.0f },
            });

            rule.GetSamplingRate(span).Should().BeApproximately(0.2f, 0.01f);
        }
    }
}
