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
}
