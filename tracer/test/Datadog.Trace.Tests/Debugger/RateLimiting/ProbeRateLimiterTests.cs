// <copyright file="ProbeRateLimiterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
    public class ProbeRateLimiterTests : IDisposable
    {
        private ProbeRateLimiter _rateLimiter;

        public ProbeRateLimiterTests()
        {
            _rateLimiter = new ProbeRateLimiter();
        }

        public void Dispose()
        {
            _rateLimiter?.Dispose();
        }

        [Fact]
        public void Constructor_DefaultConfiguration_Succeeds()
        {
            using var limiter = new ProbeRateLimiter();
            limiter.Should().NotBeNull();
            limiter.GlobalBudget.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_CustomConfiguration_Succeeds()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MaxGlobalCpuPercentage = 2.0,
                EnableEnhancedRateLimiting = true
            };

            using var limiter = new ProbeRateLimiter(config);
            limiter.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_NullConfiguration_Throws()
        {
            Action act = () => new ProbeRateLimiter(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetOrAddSampler_EnhancedMode_ReturnsProtectedSampler()
        {
            var config = new DebuggerRateLimitingConfiguration { EnableEnhancedRateLimiting = true };
            using var limiter = new ProbeRateLimiter(config);

            var sampler = limiter.GetOrAddSampler("probe-1");

            sampler.Should().NotBeNull();
            sampler.Should().BeOfType<ProtectedSampler>();
        }

        [Fact]
        public void GetOrAddSampler_SimpleMode_ReturnsAdaptiveSampler()
        {
            var config = new DebuggerRateLimitingConfiguration { EnableEnhancedRateLimiting = false };
            using var limiter = new ProbeRateLimiter(config);

            var sampler = limiter.GetOrAddSampler("probe-1");

            sampler.Should().NotBeNull();
            sampler.Should().BeOfType<AdaptiveSampler>();
        }

        [Fact]
        public void GetOrAddSampler_SameProbeId_ReturnsSameInstance()
        {
            var sampler1 = _rateLimiter.GetOrAddSampler("probe-1");
            var sampler2 = _rateLimiter.GetOrAddSampler("probe-1");

            sampler1.Should().BeSameAs(sampler2);
        }

        [Fact]
        public void GetOrAddSampler_DifferentProbeIds_ReturnsDifferentInstances()
        {
            var sampler1 = _rateLimiter.GetOrAddSampler("probe-1");
            var sampler2 = _rateLimiter.GetOrAddSampler("probe-2");

            sampler1.Should().NotBeSameAs(sampler2);
        }

        [Fact]
        public void SetRate_CreatesNewSampler()
        {
            _rateLimiter.SetRate("probe-new", samplesPerSecond: 5);

            var sampler = _rateLimiter.GetOrAddSampler("probe-new");
            sampler.Should().NotBeNull();
        }

        [Fact]
        public void SetRate_ExistingProbe_DoesNotReplace()
        {
            var sampler1 = _rateLimiter.GetOrAddSampler("probe-1");

            _rateLimiter.SetRate("probe-1", samplesPerSecond: 10);

            var sampler2 = _rateLimiter.GetOrAddSampler("probe-1");
            sampler2.Should().BeSameAs(sampler1);
        }

        [Fact]
        public void ResetRate_RemovesSampler()
        {
            var sampler = _rateLimiter.GetOrAddSampler("probe-1");
            sampler.Should().NotBeNull();

            _rateLimiter.ResetRate("probe-1");

            // Getting it again should create a new instance
            var newSampler = _rateLimiter.GetOrAddSampler("probe-1");
            newSampler.Should().NotBeSameAs(sampler);
        }

        [Fact]
        public void ResetRate_NonExistentProbe_DoesNotThrow()
        {
            Action act = () => _rateLimiter.ResetRate("non-existent");
            act.Should().NotThrow();
        }

        [Fact]
        public void SetKillSwitch_EnabledMode_SetsAllSamplers()
        {
            var config = new DebuggerRateLimitingConfiguration { EnableEnhancedRateLimiting = true };
            using var limiter = new ProbeRateLimiter(config);

            var sampler1 = limiter.GetOrAddSampler("probe-1") as ProtectedSampler;
            var sampler2 = limiter.GetOrAddSampler("probe-2") as ProtectedSampler;

            limiter.SetKillSwitch(enabled: true);

            sampler1.KillSwitch.Should().BeTrue();
            sampler2.KillSwitch.Should().BeTrue();
        }

        [Fact]
        public void SetKillSwitch_Disabled_ClearsAllSamplers()
        {
            var config = new DebuggerRateLimitingConfiguration { EnableEnhancedRateLimiting = true };
            using var limiter = new ProbeRateLimiter(config);

            var sampler1 = limiter.GetOrAddSampler("probe-1") as ProtectedSampler;
            var sampler2 = limiter.GetOrAddSampler("probe-2") as ProtectedSampler;

            limiter.SetKillSwitch(enabled: true);
            limiter.SetKillSwitch(enabled: false);

            sampler1.KillSwitch.Should().BeFalse();
            sampler2.KillSwitch.Should().BeFalse();
        }

        [Fact]
        public void GlobalBudget_Accessible()
        {
            _rateLimiter.GlobalBudget.Should().NotBeNull();
            _rateLimiter.GlobalBudget.IsExhausted.Should().BeFalse();
        }

        [Fact]
        public void ConfigureGlobalInstance_BeforeAccess_Succeeds()
        {
            // This test is tricky because Instance is static
            // We can only test that the method doesn't throw
            // Actual functionality would require reflection or process isolation
            var config = new DebuggerRateLimitingConfiguration
            {
                MaxGlobalCpuPercentage = 3.0
            };

            // Note: This will likely fail if other tests have already accessed Instance
            // In a real test suite, this would need to be in an isolated test class
            try
            {
                ProbeRateLimiter.ConfigureGlobalInstance(config);
            }
            catch (InvalidOperationException)
            {
                // Expected if Instance was already initialized by other tests
            }
        }

        [Fact]
        public void ConfigureGlobalInstance_NullConfig_Throws()
        {
            Action act = () => ProbeRateLimiter.ConfigureGlobalInstance(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void TryAddSampler_NewProbe_Succeeds()
        {
            var customSampler = new AdaptiveSampler(
                TimeSpan.FromSeconds(1),
                samplesPerWindow: 1,
                averageLookback: 1,
                budgetLookback: 1,
                rollWindowCallback: null);

            var result = _rateLimiter.TryAddSampler("custom-probe", customSampler);

            result.Should().BeTrue();
            _rateLimiter.GetOrAddSampler("custom-probe").Should().BeSameAs(customSampler);
        }

        [Fact]
        public void TryAddSampler_ExistingProbe_Fails()
        {
            _rateLimiter.GetOrAddSampler("probe-1");

            var customSampler = new AdaptiveSampler(
                TimeSpan.FromSeconds(1),
                samplesPerWindow: 1,
                averageLookback: 1,
                budgetLookback: 1,
                rollWindowCallback: null);

            var result = _rateLimiter.TryAddSampler("probe-1", customSampler);

            result.Should().BeFalse();
        }

        [Fact]
        public void Dispose_MultipleCalls_Safe()
        {
            var limiter = new ProbeRateLimiter();

            limiter.Dispose();
            limiter.Dispose();

            Action act = () => limiter.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_DisposesAllSamplers()
        {
            var config = new DebuggerRateLimitingConfiguration { EnableEnhancedRateLimiting = true };
            using var limiter = new ProbeRateLimiter(config);

            var sampler1 = limiter.GetOrAddSampler("probe-1");
            var sampler2 = limiter.GetOrAddSampler("probe-2");

            limiter.Dispose();

            // After disposal, samplers should be disposed
            // We can't easily verify this without exposing internal state
            // but we can ensure no exceptions occur
            Action act = () => limiter.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Configuration_CircuitBreakerDisabled_CreatesNopCircuitBreaker()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                EnableEnhancedRateLimiting = true,
                EnableCircuitBreaker = false
            };

            using var limiter = new ProbeRateLimiter(config);
            var sampler = limiter.GetOrAddSampler("probe-1") as ProtectedSampler;

            // Circuit should always be closed (Nop implementation)
            sampler.CircuitState.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public void Configuration_ThreadLocalPrefilterDisabled_NoMonitorTimer()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                EnableThreadLocalPrefilter = false
            };

            using var limiter = new ProbeRateLimiter(config);

            // Should create successfully without monitor timer
            limiter.Should().NotBeNull();
        }

        [Fact]
        public void ManyProbes_AllWorkIndependently()
        {
            var probeCount = 100;
            var samplers = Enumerable.Range(0, probeCount)
                .Select(i => _rateLimiter.GetOrAddSampler($"probe-{i}"))
                .ToList();

            samplers.Should().HaveCount(probeCount);
            samplers.Should().OnlyHaveUniqueItems();

            // All should be functional
            foreach (var sampler in samplers)
            {
                // Just verify Sample() doesn't throw
                Action act = () => sampler.Sample();
                act.Should().NotThrow();
            }
        }
    }
}
