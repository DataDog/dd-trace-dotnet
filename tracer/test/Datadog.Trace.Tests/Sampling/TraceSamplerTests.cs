// <copyright file="TraceSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class TraceSamplerTests
    {
        private const float FallbackRate = 0.25f;
        private const string ServiceName = "my-service-name";
        private const string Env = "my-test-env";
        private const string OperationName = "test";
        private const string ResourceName = "test-resource-name";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private static readonly Dictionary<string, float> MockAgentRates = new() { { $"service:{ServiceName},env:{Env}", FallbackRate } };

        [Fact]
        public async Task RateLimiter_Never_Applied_For_DefaultRule()
        {
            var sampler = new TraceSampler(new DenyAll());
            await RunSamplerTest(
                sampler,
                iterations: 500,
                expectedAutoKeepRate: 1,
                expectedUserKeepRate: 0,
                acceptableVariancePercent: 0);
        }

        [Fact]
        public async Task RateLimiter_Denies_All_Traces()
        {
            var sampler = new TraceSampler(new DenyAll());

            sampler.RegisterRule(
                new LocalCustomSamplingRule(
                    rate: 1,
                    patternFormat: SamplingRulesFormat.Regex,
                    serviceNamePattern: ".*",
                    operationNamePattern: ".*",
                    resourceNamePattern: ".*",
                    tagPatterns: null,
                    timeout: Timeout));

            await RunSamplerTest(
                sampler,
                iterations: 500,
                expectedAutoKeepRate: 0,
                expectedUserKeepRate: 0,
                acceptableVariancePercent: 0);
        }

        [Fact]
        public async Task Keep_Everything_Rule()
        {
            var sampler = new TraceSampler(new NoLimits());

            sampler.RegisterRule(
                new LocalCustomSamplingRule(
                    rate: 1,
                    patternFormat: SamplingRulesFormat.Regex,
                    serviceNamePattern: ".*",
                    operationNamePattern: ".*",
                    resourceNamePattern: ".*",
                    tagPatterns: null,
                    timeout: Timeout));

            await RunSamplerTest(
                sampler,
                iterations: 500,
                expectedAutoKeepRate: 0,
                expectedUserKeepRate: 1,
                acceptableVariancePercent: 0);
        }

        [Fact]
        public async Task Keep_Nothing_Rule()
        {
            var sampler = new TraceSampler(new NoLimits());

            sampler.RegisterRule(
                new LocalCustomSamplingRule(
                    rate: 0,
                    patternFormat: SamplingRulesFormat.Regex,
                    serviceNamePattern: ".*",
                    operationNamePattern: ".*",
                    resourceNamePattern: ".*",
                    tagPatterns: null,
                    timeout: Timeout));

            await RunSamplerTest(
                sampler,
                iterations: 500,
                expectedAutoKeepRate: 0,
                expectedUserKeepRate: 0,
                acceptableVariancePercent: 0);
        }

        [Fact]
        public async Task Keep_Half_Rule()
        {
            var sampler = new TraceSampler(new NoLimits());

            sampler.RegisterRule(
                new LocalCustomSamplingRule(
                    rate: 0.5f,
                    patternFormat: SamplingRulesFormat.Regex,
                    serviceNamePattern: ".*",
                    operationNamePattern: ".*",
                    resourceNamePattern: ".*",
                    tagPatterns: null,
                    timeout: Timeout));

            await RunSamplerTest(
                sampler,
                iterations: 50_000, // Higher number for lower variance
                expectedAutoKeepRate: 0,
                expectedUserKeepRate: 0.5f,
                acceptableVariancePercent: 0.05f);
        }

        [Fact]
        public async Task No_Registered_Rules_Uses_Legacy_Rates()
        {
            var sampler = new TraceSampler(new NoLimits());
            sampler.RegisterAgentSamplingRule(new AgentSamplingRule());
            sampler.SetDefaultSampleRates(MockAgentRates);

            await RunSamplerTest(
                sampler,
                iterations: 50_000, // Higher number for lower variance
                expectedAutoKeepRate: FallbackRate,
                expectedUserKeepRate: 0,
                acceptableVariancePercent: 0.05f);
        }

        [Fact]
        public async Task Choose_Between_Sampling_Mechanisms()
        {
            var settings = new TracerSettings { ServiceName = ServiceName };
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);

            using var scope = (Scope)tracer.StartActive(OperationName);
            scope.Span.Context.TraceContext.Environment = Env;

            var span = scope.Span;
            var sampler = new TraceSampler(new NoLimits());
            sampler.RegisterAgentSamplingRule(new AgentSamplingRule());

            // if there are no other rules, and before we have agent rates, mechanism is "Default"
            var (_, mechanism1) = sampler.MakeSamplingDecision(span);
            mechanism1.Should().Be(SamplingMechanism.Default);

            // add agent rates
            sampler.SetDefaultSampleRates(MockAgentRates);

            // after we have agent rates, mechanism is "AgentRate"
            var (_, mechanism2) = sampler.MakeSamplingDecision(span);
            mechanism2.Should().Be(SamplingMechanism.AgentRate);
        }

        private async Task RunSamplerTest(
            ITraceSampler sampler,
            int iterations,
            float expectedAutoKeepRate,
            float expectedUserKeepRate,
            float acceptableVariancePercent)
        {
            var sampleSize = iterations;
            var autoKeeps = 0;
            var userKeeps = 0;

            var settings = new TracerSettings { ServiceName = ServiceName };
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);

            while (sampleSize-- > 0)
            {
                using var scope = (Scope)tracer.StartActive(OperationName);
                scope.Span.Context.TraceContext.Environment = Env;
                scope.Span.ResourceName = ResourceName;

                var decision = sampler.MakeSamplingDecision(scope.Span);

                if (decision.Priority == SamplingPriorityValues.AutoKeep)
                {
                    autoKeeps++;
                }
                else if (decision.Priority == SamplingPriorityValues.UserKeep)
                {
                    userKeeps++;
                }
            }

            // AUTO_KEEP
            var autoKeepRate = autoKeeps / (float)iterations;
            var autoKeepRateLowerLimit = expectedAutoKeepRate * (1 - acceptableVariancePercent);
            var autoKeepRateUpperLimit = expectedAutoKeepRate * (1 + acceptableVariancePercent);

            Assert.True(
                autoKeepRate >= autoKeepRateLowerLimit && autoKeepRate <= autoKeepRateUpperLimit,
                $"Sampling AUTO_KEEP rate expected between {autoKeepRateLowerLimit} and {autoKeepRateUpperLimit}, actual rate is {autoKeepRate}.");

            // USER_KEEP (aka MANUAL_KEEP)
            var userKeepRate = userKeeps / (float)iterations;
            var userKeepRateLowerLimit = expectedUserKeepRate * (1 - acceptableVariancePercent);
            var userKeepRateUpperLimit = expectedUserKeepRate * (1 + acceptableVariancePercent);

            Assert.True(
                userKeepRate >= userKeepRateLowerLimit && userKeepRate <= userKeepRateUpperLimit,
                $"Sampling USER_KEEP rate expected between {userKeepRateLowerLimit} and {userKeepRateUpperLimit}, actual rate is {userKeepRate}.");
        }

        private class NoLimits : IRateLimiter
        {
            public bool Allowed(Span span)
            {
                return true;
            }

            public float GetEffectiveRate()
            {
                return 1;
            }
        }

        private class DenyAll : IRateLimiter
        {
            public bool Allowed(Span span)
            {
                return false;
            }

            public float GetEffectiveRate()
            {
                return 0;
            }
        }
    }
}
