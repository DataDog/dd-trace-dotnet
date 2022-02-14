// <copyright file="RuleBasedSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class RuleBasedSamplerTests
    {
        private const float FallbackRate = 0.25f;
        private const string ServiceName = "my-service-name";
        private const string Env = "my-test-env";
        private const string OperationName = "test";

        private static readonly Dictionary<string, float> MockAgentRates = new() { { $"service:{ServiceName},env:{Env}", FallbackRate } };

        [Fact]
        public void RateLimiter_Never_Applied_For_DefaultRule()
        {
            var sampler = new RuleBasedSampler(new DenyAll());
            RunSamplerTest(
                sampler,
                iterations: 500,
                expectedAutoKeepRate: 1,
                expectedUserKeepRate: 0,
                acceptableVariancePercent: 0);
        }

        [Fact]
        public void RateLimiter_Denies_All_Traces()
        {
            var sampler = new RuleBasedSampler(new DenyAll());
            sampler.RegisterRule(new CustomSamplingRule(1, "Allow_all", ".*", ".*"));
            RunSamplerTest(
                sampler,
                iterations: 500,
                expectedAutoKeepRate: 0,
                expectedUserKeepRate: 0,
                acceptableVariancePercent: 0);
        }

        [Fact]
        public void Keep_Everything_Rule()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.RegisterRule(new CustomSamplingRule(1, "Allow_all", ".*", ".*"));
            RunSamplerTest(
                sampler,
                iterations: 500,
                expectedAutoKeepRate: 0,
                expectedUserKeepRate: 1,
                acceptableVariancePercent: 0);
        }

        [Fact]
        public void Keep_Nothing_Rule()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.RegisterRule(new CustomSamplingRule(0, "Allow_nothing", ".*", ".*"));
            RunSamplerTest(
                sampler,
                iterations: 500,
                expectedAutoKeepRate: 0,
                expectedUserKeepRate: 0,
                acceptableVariancePercent: 0);
        }

        [Fact]
        public void Keep_Half_Rule()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.RegisterRule(new CustomSamplingRule(0.5f, "Allow_half", ".*", ".*"));
            RunSamplerTest(
                sampler,
                iterations: 50_000, // Higher number for lower variance
                expectedAutoKeepRate: 0,
                expectedUserKeepRate: 0.5f,
                acceptableVariancePercent: 0.05f);
        }

        [Fact]
        public void No_Registered_Rules_Uses_Legacy_Rates()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.SetDefaultSampleRates(MockAgentRates);

            RunSamplerTest(
                sampler,
                iterations: 50_000, // Higher number for lower variance
                expectedAutoKeepRate: FallbackRate,
                expectedUserKeepRate: 0,
                acceptableVariancePercent: 0.05f);
        }

        private static Span GetMyServiceSpan(ulong traceId)
        {
            var span = new Span(new SpanContext(traceId, spanId: 1, null, serviceName: ServiceName), DateTimeOffset.Now) { OperationName = OperationName };
            span.SetTag(Tags.Env, Env);
            return span;
        }

        private void RunSamplerTest(
            ISampler sampler,
            int iterations,
            float expectedAutoKeepRate,
            float expectedUserKeepRate,
            float acceptableVariancePercent)
        {
            var sampleSize = iterations;
            var autoKeeps = 0;
            var userKeeps = 0;
            int seed = new Random().Next();
            var idGenerator = new SpanIdGenerator(seed);

            while (sampleSize-- > 0)
            {
                var traceId = idGenerator.CreateNew();
                var span = GetMyServiceSpan(traceId);
                var decision = sampler.MakeSamplingDecision(span);

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
                $"Sampling AUTO_KEEP rate expected between {autoKeepRateLowerLimit} and {autoKeepRateUpperLimit}, actual rate is {autoKeepRate}. Random generator seeded with {seed}.");

            // USER_KEEP (aka MANUAL_KEEP)
            var userKeepRate = userKeeps / (float)iterations;
            var userKeepRateLowerLimit = expectedUserKeepRate * (1 - acceptableVariancePercent);
            var userKeepRateUpperLimit = expectedUserKeepRate * (1 + acceptableVariancePercent);

            Assert.True(
                userKeepRate >= userKeepRateLowerLimit && userKeepRate <= userKeepRateUpperLimit,
                $"Sampling USER_KEEP rate expected between {userKeepRateLowerLimit} and {userKeepRateUpperLimit}, actual rate is {userKeepRate}. Random generator seeded with {seed}.");
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
