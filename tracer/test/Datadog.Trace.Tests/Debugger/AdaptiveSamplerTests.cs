// <copyright file="AdaptiveSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
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
    [InlineData(300)]
    [InlineData(3600)]
    public void Create_RateBelowOnePerSecond_AdmitsOneSamplePerInterval(int secondsPerSample)
    {
        using var sampler = (AdaptiveSampler)AdaptiveSamplerLifetime.Create(1.0 / secondsPerSample);
        var interval = Stopwatch.Frequency * secondsPerSample;
        sampler.GetInternalState().SampleInterval.Should().Be(interval);

        var start = Stopwatch.GetTimestamp();
        sampler.SampleAt(start).Should().BeTrue();
        sampler.SampleAt(start + 1).Should().BeFalse();
        sampler.SampleAt(start + interval - 1).Should().BeFalse();
        sampler.SampleAt(start + interval).Should().BeTrue();
        sampler.SampleAt(start + interval + 1).Should().BeFalse();
    }

    [Fact]
    public void Sample_RateBelowOnePerSecond_AdmitsOneOfManyConcurrentCallers()
    {
        const int threadCount = 8;
        using var sampler = AdaptiveSamplerLifetime.Create(1.0 / 300);
        using var allStarted = new Barrier(threadCount);
        var admitted = 0;

        var threads = new Thread[threadCount];
        for (var t = 0; t < threadCount; t++)
        {
            threads[t] = new Thread(() =>
            {
                // Release all threads together so the first calls race for the slot.
                allStarted.SignalAndWait();
                for (var i = 0; i < 1_000; i++)
                {
                    if (sampler.Sample())
                    {
                        Interlocked.Increment(ref admitted);
                    }
                }
            });
            threads[t].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        admitted.Should().Be(1);
    }

    [Theory]
    [InlineData(1d, 1)]
    [InlineData(2.5d, 2)]
    [InlineData(100d, 100)]
    [InlineData(1e10, int.MaxValue)]
    [InlineData(double.PositiveInfinity, int.MaxValue)]
    [InlineData(0d, 0)]
    [InlineData(-1d, 0)]
    [InlineData(double.NaN, 0)]
    public void SetRate_RateNotBetweenZeroAndOne_KeepsWindowSampling(double samplesPerSecond, int samplesPerWindow)
    {
        const int budgetLookback = 4;
        using var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 0, averageLookback: 1, budgetLookback: budgetLookback, rollWindowCallback: null);

        sampler.SetRate(samplesPerSecond);

        var state = sampler.GetInternalState();
        state.SampleInterval.Should().Be(0);
        state.Budget.Should().Be(samplesPerWindow + ((long)budgetLookback * samplesPerWindow));
        sampler.Sample().Should().Be(samplesPerWindow > 0);
    }

    [Fact]
    public void SetRate_SwitchesBetweenWindowAndIntervalSamplingInPlace()
    {
        const int budgetLookback = 4;
        using var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 2, averageLookback: 1, budgetLookback: budgetLookback, rollWindowCallback: null);

        sampler.SetRate(1.0 / 300);
        var fiveMinutes = Stopwatch.Frequency * 300;
        sampler.GetInternalState().SampleInterval.Should().Be(fiveMinutes);

        var start = Stopwatch.GetTimestamp();
        sampler.SampleAt(start).Should().BeTrue();

        // A new interval counts from the last sample.
        sampler.SetRate(1.0 / 3600);
        sampler.SampleAt(start + fiveMinutes).Should().BeFalse();
        sampler.SampleAt(start + (Stopwatch.Frequency * 3600)).Should().BeTrue();

        sampler.SetRate(5);
        var state = sampler.GetInternalState();
        state.SampleInterval.Should().Be(0);
        state.Budget.Should().Be(5 + (budgetLookback * 5));
        sampler.Sample().Should().BeTrue();
        sampler.Sample().Should().BeTrue();

        // Coming back from window sampling starts a new gate, which admits the next call.
        sampler.SetRate(1.0 / 300);
        sampler.SampleAt(start).Should().BeTrue();
        sampler.SampleAt(start + 1).Should().BeFalse();
    }

    [Fact]
    public void SetRate_RateTooSmallForTheInterval_CapsItInsteadOfOverflowing()
    {
        using var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 1, averageLookback: 1, budgetLookback: 1, rollWindowCallback: null);

        sampler.SetRate(double.Epsilon);

        sampler.GetInternalState().SampleInterval.Should().Be(long.MaxValue);
        var start = Stopwatch.GetTimestamp();
        sampler.SampleAt(start).Should().BeTrue();
        sampler.SampleAt(start + (Stopwatch.Frequency * 3600 * 24 * 365)).Should().BeFalse();
    }

    [Fact]
    public void RollWindow_IntervalSampling_LeavesWindowStateUntilWindowSamplingResumes()
    {
        var windowRolls = 0;
        using var sampler = new AdaptiveSampler(WindowDuration, samplesPerWindow: 2, averageLookback: 1, budgetLookback: 1, rollWindowCallback: () => windowRolls++);
        sampler.Keep();
        sampler.Drop();
        sampler.RollWindow();
        var windowState = sampler.GetInternalState();

        sampler.SetRate(0.5);
        sampler.Drop();
        sampler.RollWindow();

        windowRolls.Should().Be(1);
        var intervalState = sampler.GetInternalState();
        intervalState.TestCount.Should().Be(1);
        intervalState.Budget.Should().Be(windowState.Budget);
        intervalState.TotalAverage.Should().Be(windowState.TotalAverage);
        intervalState.Probability.Should().Be(windowState.Probability);

        sampler.SetRate(2);
        sampler.RollWindow();

        windowRolls.Should().Be(2);
        sampler.GetInternalState().TestCount.Should().Be(0);
    }
}
