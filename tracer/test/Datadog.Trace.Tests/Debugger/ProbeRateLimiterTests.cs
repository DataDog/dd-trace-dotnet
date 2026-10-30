// <copyright file="ProbeRateLimiterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ProbeRateLimiterTests
{
    [Fact]
    public void SetRate_UpdatesExistingSamplerInPlace()
    {
        var limiter = ProbeRateLimiter.Instance;
        var probeId = $"setrate-update-{Guid.NewGuid():N}";

        try
        {
            var initial = new AdaptiveSampler(Timeout.InfiniteTimeSpan, samplesPerWindow: 1, averageLookback: 180, budgetLookback: 16, rollWindowCallback: null);
            limiter.TryAddSampler(probeId, initial).Should().BeTrue();

            // SetRate must mutate in place; replacing would wipe running EMAs.
            limiter.SetRate(probeId, snapshotsPerSecond: 100);
            var afterUpdate = limiter.GerOrAddSampler(probeId);

            afterUpdate.Should().BeSameAs(initial);

            // Budget = samplesPerWindow + (budgetLookback * samplesPerWindow); budgetLookback = 16.
            var state = ((AdaptiveSampler)afterUpdate).GetInternalState();
            state.Budget.Should().Be(100 + (16 * 100));
        }
        finally
        {
            limiter.ResetRate(probeId);
        }
    }

    [Fact]
    public void SetRate_RateBelowOnePerSecond_UpdatesExistingSamplerInPlace()
    {
        var limiter = new ProbeRateLimiter(AdaptiveSamplerLifetime.Create);
        const string probeId = "below-one-per-second";

        try
        {
            limiter.SetRate(probeId, 1.0 / 3600);
            var sampler = (AdaptiveSampler)limiter.GerOrAddSampler(probeId);
            sampler.GetInternalState().SampleInterval.Should().Be(Stopwatch.Frequency * 3600);

            limiter.SetRate(probeId, 1.0 / 300);

            // ProbeData caches the sampler, so the new rate must apply to the same instance.
            limiter.GerOrAddSampler(probeId).Should().BeSameAs(sampler);
            sampler.GetInternalState().SampleInterval.Should().Be(Stopwatch.Frequency * 300);
            sampler.Sample().Should().BeTrue();
            sampler.Sample().Should().BeFalse();
        }
        finally
        {
            limiter.ResetRate(probeId);
        }
    }

    [Fact]
    public void ResetRate_DisposesRemovedSampler()
    {
        var limiter = ProbeRateLimiter.Instance;
        var probeId = $"resetrate-dispose-{Guid.NewGuid():N}";
        var tracker = new TrackingSampler();

        Assert.True(limiter.TryAddSampler(probeId, tracker));

        limiter.ResetRate(probeId);

        // Without disposal, an AdaptiveSampler's Timer would leak on every probe removal.
        Assert.True(tracker.Disposed);
    }

    [Fact]
    public void ResetRate_DisposesRemovedSamplerFromFactory()
    {
        var factory = new RecordingSamplerFactory();
        var limiter = new ProbeRateLimiter(factory.Create);

        _ = limiter.GerOrAddSampler("probe");
        var sampler = factory.Samplers[0];

        limiter.ResetRate("probe");

        Assert.Equal(1, sampler.DisposeCallCount);
    }

    [Fact]
    public void TryAddSampler_DisposesRejectedSampler()
    {
        var factory = new RecordingSamplerFactory();
        var limiter = new ProbeRateLimiter(factory.Create);
        _ = limiter.GerOrAddSampler("probe");
        var rejectedSampler = new TestAdaptiveSampler();

        var added = limiter.TryAddSampler("probe", rejectedSampler);

        Assert.False(added);
        Assert.Equal(1, rejectedSampler.DisposeCallCount);
    }

    [Fact]
    public void GerOrAddSampler_ReturnsExistingEntryWithoutLeakingCandidate()
    {
        var limiter = ProbeRateLimiter.Instance;
        var probeId = $"getoradd-existing-{Guid.NewGuid():N}";
        var preExisting = new TrackingSampler();

        Assert.True(limiter.TryAddSampler(probeId, preExisting));

        try
        {
            var resolved = limiter.GerOrAddSampler(probeId);

            Assert.Same(preExisting, resolved);
            Assert.False(preExisting.Disposed);
        }
        finally
        {
            limiter.ResetRate(probeId);
        }
    }

    private sealed class RecordingSamplerFactory
    {
        public List<TestAdaptiveSampler> Samplers { get; } = [];

        public IAdaptiveSampler Create(double snapshotsPerSecond)
        {
            var sampler = new TestAdaptiveSampler();
            Samplers.Add(sampler);
            return sampler;
        }
    }

    private sealed class TestAdaptiveSampler : IAdaptiveSampler
    {
        public int DisposeCallCount { get; private set; }

        public bool Sample() => true;

        public bool Keep() => true;

        public bool Drop() => false;

        public double NextDouble() => 0;

        public void Dispose()
        {
            DisposeCallCount++;
        }
    }

    private sealed class TrackingSampler : IAdaptiveSampler
    {
        public bool Disposed { get; private set; }

        public bool Sample() => true;

        public bool Keep() => true;

        public bool Drop() => true;

        public double NextDouble() => 1.0;

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
