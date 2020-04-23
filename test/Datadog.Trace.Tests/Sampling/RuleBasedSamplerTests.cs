using System;
using System.Collections.Generic;
using Datadog.Trace.Sampling;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class RuleBasedSamplerTests
    {
        private static readonly float FallbackRate = 0.25f;
        private static readonly string ServiceName = "my-service-name";
        private static readonly string Env = "my-test-env";
        private static readonly string OperationName = "test";
        private static readonly IEnumerable<KeyValuePair<string, float>> MockAgentRates = new List<KeyValuePair<string, float>>() { new KeyValuePair<string, float>($"service:{ServiceName},env:{Env}", FallbackRate) };

        private static ulong _id = 1;

        [Fact]
        public void RateLimiter_Denies_All_Traces()
        {
            var sampler = new RuleBasedSampler(new DenyAll());
            sampler.RegisterRule(new CustomSamplingRule(1, "Allow_all", ".*", ".*"));
            RunSamplerTest(
                sampler,
                500,
                0,
                0);
        }

        [Fact]
        public void Keep_Everything_Rule()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.RegisterRule(new CustomSamplingRule(1, "Allow_all", ".*", ".*"));
            RunSamplerTest(
                sampler,
                500,
                1,
                0);
        }

        [Fact]
        public void Keep_Nothing_Rule()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.RegisterRule(new CustomSamplingRule(0, "Allow_nothing", ".*", ".*"));
            RunSamplerTest(
                sampler,
                500,
                0,
                0);
        }

        [Fact]
        public void Keep_Half_Rule()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.RegisterRule(new CustomSamplingRule(0.5f, "Allow_nothing", ".*", ".*"));
            RunSamplerTest(
                sampler,
                10_000, // Higher number for lower variance
                0.5f,
                0.05f);
        }

        [Fact]
        public void No_Registered_Rules_Uses_Legacy_Rates()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.SetDefaultSampleRates(MockAgentRates);

            RunSamplerTest(
                sampler,
                10_000, // Higher number for lower variance
                FallbackRate,
                0.05f);
        }

        private static Span GetMyServiceSpan()
        {
            var span = new SpanImplementation(new SpanContext(_id++, _id++, null, serviceName: ServiceName), DateTimeOffset.Now) { OperationName = OperationName };
            span.SetTag(Tags.Env, Env);
            return span;
        }

        private void RunSamplerTest(
            ISampler sampler,
            int iterations,
            float expectedAutoKeepRate,
            float acceptableVariancePercent)
        {
            var sampleSize = iterations;
            var autoKeeps = 0;
            while (sampleSize-- > 0)
            {
                var span = GetMyServiceSpan();
                var priority = sampler.GetSamplingPriority(span);
                if (priority == SamplingPriority.AutoKeep)
                {
                    autoKeeps++;
                }
            }

            var autoKeepRate = autoKeeps / (float)iterations;

            var lowerLimit = expectedAutoKeepRate * (1 - acceptableVariancePercent);
            var upperLimit = expectedAutoKeepRate * (1 + acceptableVariancePercent);

            Assert.True(
                autoKeepRate >= lowerLimit && autoKeepRate <= upperLimit,
                $"Expected between {lowerLimit} and {upperLimit}, actual rate is {autoKeepRate}.");
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
