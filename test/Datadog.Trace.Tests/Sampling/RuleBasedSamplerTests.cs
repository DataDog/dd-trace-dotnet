using System;
using Datadog.Trace.Sampling;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    [Collection(nameof(Datadog.Trace.Tests.Sampling))]
    public class RuleBasedSamplerTests
    {
        [Fact]
        public void Keep_Everything_Rule()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.RegisterRule(new RegexSamplingRule(1, "Allow_all", ".*", ".*"));
            RunSamplerTest(
                sampler,
                500,
                1,
                0,
                () => Tracer.Instance.StartActive(operationName: "test"));
        }

        [Fact]
        public void Keep_Nothing_Rule()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.RegisterRule(new RegexSamplingRule(0, "Allow_nothing", ".*", ".*"));
            RunSamplerTest(
                sampler,
                500,
                0,
                0,
                () => Tracer.Instance.StartActive(operationName: "test"));
        }

        [Fact]
        public void Keep_Half_Rule()
        {
            var sampler = new RuleBasedSampler(new NoLimits());
            sampler.RegisterRule(new RegexSamplingRule(0.5f, "Allow_nothing", ".*", ".*"));
            RunSamplerTest(
                sampler,
                10_000, // Higher number for lower variance
                0.5f,
                0.05f,
                () => Tracer.Instance.StartActive(operationName: "test"));
        }

        private void RunSamplerTest(
            ISampler sampler,
            int iterations,
            float expectedAutoKeepRate,
            float acceptableVariancePercent,
            Func<Scope> scopeFactory)
        {
            var sampleSize = iterations;
            var autoKeeps = 0;
            while (sampleSize-- > 0)
            {
                using (var scope = scopeFactory())
                {
                    var priority = sampler.GetSamplingPriority(scope.Span);
                    if (priority == SamplingPriority.AutoKeep)
                    {
                        autoKeeps++;
                    }
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
            public bool Allowed(ulong traceId)
            {
                return true;
            }

            public float GetEffectiveRate()
            {
                return 1;
            }
        }
    }
}
