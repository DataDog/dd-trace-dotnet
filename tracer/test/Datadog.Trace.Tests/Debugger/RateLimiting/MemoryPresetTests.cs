// <copyright file="MemoryPresetTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
    public class MemoryPresetTests
    {
        [Fact]
        public void DefaultConfiguration_UsesAutoPreset()
        {
            // Default configuration should use Auto preset
            var config = new DebuggerRateLimitingConfiguration();

            config.MemoryPreset.Should().Be(MemoryPreset.Auto);
            config.MaxMemoryUsagePercent.Should().Be(0);
            config.MaxGen2CollectionsPerSecond.Should().Be(0);
        }

        [Fact]
        public void StandardPreset_AppliesStandardThresholds()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MemoryPreset = MemoryPreset.Standard,
                MaxMemoryUsagePercent = 0,
                MaxGen2CollectionsPerSecond = 0
            };

            using var limiter = new ProbeRateLimiter(config);

            var monitor = limiter.MemoryPressureMonitor;
            monitor.Should().NotBeNull();
            monitor!.HighPressureThreshold.Should().BeApproximately(0.92, 0.001);
            monitor.MaxGen2PerSecond.Should().Be(3);
        }

        [Fact]
        public void ConstrainedPreset_AppliesHighThresholds()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MemoryPreset = MemoryPreset.Constrained,
                MaxMemoryUsagePercent = 0,
                MaxGen2CollectionsPerSecond = 0
            };

            using var limiter = new ProbeRateLimiter(config);

            var monitor = limiter.MemoryPressureMonitor;
            monitor.Should().NotBeNull();
            monitor!.HighPressureThreshold.Should().BeApproximately(0.99, 0.001);
            monitor.MaxGen2PerSecond.Should().Be(200);
        }

        [Fact]
        public void AggressivePreset_AppliesConservativeThresholds()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MemoryPreset = MemoryPreset.Aggressive,
                MaxMemoryUsagePercent = 0,
                MaxGen2CollectionsPerSecond = 0
            };

            using var limiter = new ProbeRateLimiter(config);

            var monitor = limiter.MemoryPressureMonitor;
            monitor.Should().NotBeNull();
            monitor!.HighPressureThreshold.Should().BeApproximately(0.87, 0.001);
            monitor.MaxGen2PerSecond.Should().Be(1);
        }

        [Fact]
        public void ExplicitThresholds_OverridePreset()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MemoryPreset = MemoryPreset.Standard,
                MaxMemoryUsagePercent = 95.0, // Explicit value
                MaxGen2CollectionsPerSecond = 10 // Explicit value
            };

            using var limiter = new ProbeRateLimiter(config);

            var monitor = limiter.MemoryPressureMonitor;
            monitor.Should().NotBeNull();
            monitor!.HighPressureThreshold.Should().BeApproximately(0.95, 0.001);
            monitor.MaxGen2PerSecond.Should().Be(10);
        }

        [Fact]
        public void PartialExplicitThresholds_UsesPresetForRest()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MemoryPreset = MemoryPreset.Constrained,
                MaxMemoryUsagePercent = 95.0, // Explicit value
                MaxGen2CollectionsPerSecond = 0 // Use preset default
            };

            using var limiter = new ProbeRateLimiter(config);

            var monitor = limiter.MemoryPressureMonitor;
            monitor.Should().NotBeNull();
            monitor!.HighPressureThreshold.Should().BeApproximately(0.95, 0.001);
            monitor.MaxGen2PerSecond.Should().Be(200);
        }

        [Fact]
        public void Configuration_ValidatesMemoryPercentRange()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MaxMemoryUsagePercent = 101 // Invalid: > 100
            };

            // Should throw during validation
            Action act = () => config.Validate();
            act.Should()
               .Throw<ArgumentException>()
               .WithMessage("*MaxMemoryUsagePercent*");
        }

        [Fact]
        public void Configuration_ValidatesNegativeMemoryPercent()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MaxMemoryUsagePercent = -1 // Invalid: < 0
            };

            // Should throw during validation
            Action act = () => config.Validate();
            act.Should()
               .Throw<ArgumentException>()
               .WithMessage("*MaxMemoryUsagePercent*");
        }

        [Fact]
        public void Configuration_AcceptsZeroAsAuto()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MaxMemoryUsagePercent = 0,
                MaxGen2CollectionsPerSecond = 0
            };

            // Should not throw
            Action act = () => config.Validate();
            act.Should().NotThrow();
        }

        [Fact]
        public void Configuration_ValidatesNegativeGen2Collections()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                MaxGen2CollectionsPerSecond = -1 // Invalid: < 0
            };

            // Should throw during validation
            Action act = () => config.Validate();
            act.Should()
               .Throw<ArgumentException>()
               .WithMessage("*MaxGen2CollectionsPerSecond*");
        }

        [Fact]
        public void MemoryPressureMonitoring_CanBeDisabled()
        {
            var config = new DebuggerRateLimitingConfiguration
            {
                EnableMemoryPressureMonitoring = false
            };

            using var limiter = new ProbeRateLimiter(config);

            limiter.Should().NotBeNull();
            limiter.MemoryPressureMonitor.Should().BeNull();
        }
    }
}
