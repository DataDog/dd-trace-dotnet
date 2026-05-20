// <copyright file="ProbeRateLimiterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Debugger.RateLimiting;
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
            Assert.True(limiter.TryAddSampler(probeId, initial));

            // SetRate must mutate in place; replacing would wipe running EMAs.
            limiter.SetRate(probeId, samplesPerSecond: 100);
            var afterUpdate = limiter.GerOrAddSampler(probeId);

            Assert.Same(initial, afterUpdate);

            // Budget = samplesPerWindow + (budgetLookback * samplesPerWindow); budgetLookback = 16.
            var state = ((AdaptiveSampler)afterUpdate).GetInternalState();
            Assert.Equal(expected: 100 + (16 * 100), state.Budget);
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
