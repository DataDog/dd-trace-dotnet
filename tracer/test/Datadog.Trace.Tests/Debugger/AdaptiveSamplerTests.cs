// <copyright file="AdaptiveSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Debugger.RateLimiting;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class AdaptiveSamplerTests
{
    private static readonly TimeSpan WindowDuration = Timeout.InfiniteTimeSpan;

    [Fact]
    public void TestKeep()
    {
        var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 1, averageLookback: 1, budgetLookback: 1, rollWindowCallback: null);

        var initialState = sampler.GetInternalState();

        Assert.True(sampler.Keep());

        var newState = sampler.GetInternalState();

        Assert.Equal(initialState.TestCount + 1, newState.TestCount);
        Assert.Equal(initialState.SampleCount + 1, newState.SampleCount);
    }

    [Fact]
    public void TestDrop()
    {
        var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 1, averageLookback: 1, budgetLookback: 1, rollWindowCallback: null);

        var initialState = sampler.GetInternalState();

        Assert.False(sampler.Drop());

        var newState = sampler.GetInternalState();

        Assert.Equal(initialState.TestCount + 1, newState.TestCount);
        Assert.Equal(initialState.SampleCount, newState.SampleCount);
    }

    [Fact]
    public void TestRollWindow()
    {
        var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 2, averageLookback: 1, budgetLookback: 1, rollWindowCallback: null);

        var state = sampler.GetInternalState();

        Assert.Equal(expected: 0, state.TestCount);
        Assert.Equal(expected: 0, state.SampleCount);
        Assert.Equal(expected: 4, state.Budget);
        Assert.Equal(expected: 0.0, state.TotalAverage);
        Assert.Equal(expected: 1.0, state.Probability);

        sampler.Keep();
        sampler.Drop();

        state = sampler.GetInternalState();
        Assert.Equal(expected: 2, state.TestCount);
        Assert.Equal(expected: 1, state.SampleCount);

        sampler.RollWindow();

        state = sampler.GetInternalState();
        Assert.Equal(expected: 0, state.TestCount);
        Assert.Equal(expected: 0, state.SampleCount);
        Assert.Equal(expected: 1, state.Budget);
        Assert.Equal(expected: 2.0, state.TotalAverage);
        Assert.Equal(expected: 0.5, state.Probability);

        sampler.Keep();
        sampler.Keep();
        sampler.Drop();

        state = sampler.GetInternalState();
        Assert.Equal(expected: 3, state.TestCount);
        Assert.Equal(expected: 2, state.SampleCount);
        Assert.Equal(expected: 1, state.Budget);
        Assert.Equal(expected: 2.0, state.TotalAverage);
        Assert.Equal(expected: 0.5, state.Probability);

        sampler.RollWindow();

        state = sampler.GetInternalState();
        Assert.Equal(expected: 0, state.TestCount);
        Assert.Equal(expected: 0, state.SampleCount);
        Assert.Equal(expected: 0, state.Budget);
        Assert.Equal(expected: 3.0, state.TotalAverage);
        Assert.Equal(expected: 0.0, state.Probability);

        sampler.Drop();
        sampler.Drop();
        sampler.Drop();

        state = sampler.GetInternalState();
        Assert.Equal(expected: 3, state.TestCount);
        Assert.Equal(expected: 0, state.SampleCount);
        Assert.Equal(expected: 0, state.Budget);
        Assert.Equal(expected: 3.0, state.TotalAverage);
        Assert.Equal(expected: 0.0, state.Probability);

        sampler.RollWindow();

        state = sampler.GetInternalState();
        Assert.Equal(expected: 0, state.TestCount);
        Assert.Equal(expected: 0, state.SampleCount);
        Assert.Equal(expected: 2, state.Budget);
        Assert.Equal(expected: 3.0, state.TotalAverage);
        Assert.Equal(2.0 / 3.0, state.Probability);
    }

    [Fact]
    public void SetRate_UpdatesBudgetWithoutResettingHistory()
    {
        const int initialRate = 2;
        const int budgetLookback = 4;
        var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: initialRate, averageLookback: 1, budgetLookback: budgetLookback, rollWindowCallback: null);

        // Accumulate running stats; SetRate must preserve them (not behave like a re-create).
        sampler.Keep();
        sampler.Drop();
        sampler.RollWindow();

        var beforeUpdate = sampler.GetInternalState();
        Assert.NotEqual(expected: 0.0, beforeUpdate.TotalAverage);

        const int newRate = 50;
        sampler.SetRate(newRate);

        var afterUpdate = sampler.GetInternalState();
        Assert.Equal(newRate + (budgetLookback * newRate), afterUpdate.Budget);
        Assert.Equal(beforeUpdate.TotalAverage, afterUpdate.TotalAverage);
        Assert.Equal(beforeUpdate.Probability, afterUpdate.Probability);
    }

    [Fact]
    public void Dispose_IsIdempotentAndStopsTimer()
    {
        var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 1, averageLookback: 1, budgetLookback: 1, rollWindowCallback: null);

        sampler.Dispose();
        sampler.Dispose();
    }

    [Theory]
    [InlineData(0d, 1, 0)]
    [InlineData(0.5d, 2, 1)]
    [InlineData(2d, 1, 2)]
    [InlineData(2.5d, 2, 5)]
    [InlineData(0.1d, 10, 1)]
    public void Create_ScalesWindowSoFractionalRatesAreNotTruncated(double snapshotsPerSecond, int windowSeconds, int samplesPerWindow)
    {
        var sampler = (AdaptiveSampler)AdaptiveSamplerLifetime.Create(snapshotsPerSecond);
        try
        {
            Assert.Equal(TimeSpan.FromSeconds(windowSeconds), sampler.WindowDuration);

            // AdaptiveSamplerLifetime uses a budget lookback of 16.
            Assert.Equal(samplesPerWindow + (16 * samplesPerWindow), sampler.GetInternalState().Budget);
            Assert.Equal(snapshotsPerSecond > 0, sampler.Sample());
        }
        finally
        {
            sampler.Dispose();
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1d)]
    public void Create_NonPositiveRate_BlocksSnapshots(double snapshotsPerSecond)
    {
        var sampler = (AdaptiveSampler)AdaptiveSamplerLifetime.Create(snapshotsPerSecond);
        try
        {
            Assert.Equal(TimeSpan.FromSeconds(1), sampler.WindowDuration);
            Assert.Equal(0, sampler.GetInternalState().Budget);
            Assert.False(sampler.Sample());
        }
        finally
        {
            sampler.Dispose();
        }
    }

    [Fact]
    public void SetSnapshotsPerSecond_IntegerRateKeepsWindowAndHistory()
    {
        const int budgetLookback = 4;
        var sampler = new AdaptiveSampler(TimeSpan.FromSeconds(1), samplesPerWindow: 2, averageLookback: 1, budgetLookback: budgetLookback, rollWindowCallback: null);
        try
        {
            sampler.Keep();
            sampler.Drop();
            sampler.RollWindow();
            var before = sampler.GetInternalState();

            sampler.SetSnapshotsPerSecond(50);

            var after = sampler.GetInternalState();
            Assert.Equal(TimeSpan.FromSeconds(1), sampler.WindowDuration);
            Assert.Equal(50 + (budgetLookback * 50), after.Budget);
            Assert.Equal(before.TotalAverage, after.TotalAverage);
            Assert.Equal(before.Probability, after.Probability);
        }
        finally
        {
            sampler.Dispose();
        }
    }

    [Fact]
    public void SetSnapshotsPerSecond_FractionalRateScalesWindowAndResetsHistory()
    {
        const int budgetLookback = 4;
        var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 2, averageLookback: 1, budgetLookback: budgetLookback, rollWindowCallback: null);
        try
        {
            sampler.Keep();
            sampler.Drop();
            sampler.RollWindow();
            Assert.NotEqual(0, sampler.GetInternalState().TotalAverage);

            sampler.SetSnapshotsPerSecond(0.5);

            Assert.Equal(TimeSpan.FromSeconds(2), sampler.WindowDuration);
            var state = sampler.GetInternalState();
            Assert.Equal(1 + (budgetLookback * 1), state.Budget);
            Assert.Equal(0, state.TotalAverage);
            Assert.Equal(1, state.Probability);
            Assert.True(sampler.Sample());
        }
        finally
        {
            sampler.Dispose();
        }
    }
}
