// <copyright file="DebuggerGlobalRateLimiterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class DebuggerGlobalRateLimiterTests
{
    [Fact]
    public void Constructor_UsesFallbackRates()
    {
        var factory = new RecordingSamplerFactory();

        _ = new DebuggerGlobalRateLimiter(factory.Create, new NullLogRateLimiter());

        Assert.Equal(
            [DebuggerGlobalRateLimiter.DefaultSnapshotSamplesPerSecond, DebuggerGlobalRateLimiter.DefaultLogSamplesPerSecond],
            factory.RequestedRates);
    }

    [Fact]
    public void SetRate_UsesConfiguredRateForSnapshotAndLogSamplers()
    {
        var factory = new RecordingSamplerFactory();
        var limiter = new DebuggerGlobalRateLimiter(factory.Create, new NullLogRateLimiter());

        limiter.SetRate(42);

        Assert.Equal(
            [DebuggerGlobalRateLimiter.DefaultSnapshotSamplesPerSecond, DebuggerGlobalRateLimiter.DefaultLogSamplesPerSecond, 42, 42],
            factory.RequestedRates);
    }

    [Fact]
    public void SetRate_DisposesPreviousSamplers()
    {
        var factory = new RecordingSamplerFactory();
        var limiter = new DebuggerGlobalRateLimiter(factory.Create, new NullLogRateLimiter());
        var initialSnapshotSampler = factory.Samplers[0];
        var initialLogSampler = factory.Samplers[1];

        limiter.SetRate(42);

        Assert.Equal(1, initialSnapshotSampler.DisposeCallCount);
        Assert.Equal(1, initialLogSampler.DisposeCallCount);
    }

    [Fact]
    public void ResetRate_RestoresFallbackRates()
    {
        var factory = new RecordingSamplerFactory();
        var limiter = new DebuggerGlobalRateLimiter(factory.Create, new NullLogRateLimiter());
        limiter.SetRate(42);

        limiter.ResetRate();

        Assert.Equal(
            [DebuggerGlobalRateLimiter.DefaultSnapshotSamplesPerSecond, DebuggerGlobalRateLimiter.DefaultLogSamplesPerSecond, 42, 42, DebuggerGlobalRateLimiter.DefaultSnapshotSamplesPerSecond, DebuggerGlobalRateLimiter.DefaultLogSamplesPerSecond],
            factory.RequestedRates);
    }

    [Fact]
    public void Dispose_DisposesCurrentSamplers()
    {
        var factory = new RecordingSamplerFactory();
        var limiter = new DebuggerGlobalRateLimiter(factory.Create, new NullLogRateLimiter());
        limiter.SetRate(42);
        var currentSnapshotSampler = factory.Samplers[2];
        var currentLogSampler = factory.Samplers[3];

        limiter.Dispose();

        Assert.Equal(1, currentSnapshotSampler.DisposeCallCount);
        Assert.Equal(1, currentLogSampler.DisposeCallCount);
    }

    [Fact]
    public void SnapshotProbesShareOneGlobalBudget()
    {
        var factory = new RecordingSamplerFactory();
        var limiter = new DebuggerGlobalRateLimiter(factory.Create, new NullLogRateLimiter());
        factory.Samplers[0].SetResults(true, false);

        var firstResult = limiter.ShouldSample(ProbeType.Snapshot, "snapshot-1");
        var secondResult = limiter.ShouldSample(ProbeType.Snapshot, "snapshot-2");

        Assert.True(firstResult);
        Assert.False(secondResult);
        Assert.Equal(2, factory.Samplers[0].SampleCallCount);
        Assert.Equal(0, factory.Samplers[1].SampleCallCount);
    }

    [Fact]
    public void NonPayloadProbesAreUnaffected()
    {
        var factory = new RecordingSamplerFactory();
        var limiter = new DebuggerGlobalRateLimiter(factory.Create, new NullLogRateLimiter());

        var metricResult = limiter.ShouldSample(ProbeType.Metric, "metric");
        var spanDecorationResult = limiter.ShouldSample(ProbeType.SpanDecoration, "span");

        Assert.True(metricResult);
        Assert.True(spanDecorationResult);
        Assert.Equal(0, factory.Samplers[0].SampleCallCount);
        Assert.Equal(0, factory.Samplers[1].SampleCallCount);
    }

    private sealed class RecordingSamplerFactory
    {
        public List<int> RequestedRates { get; } = [];

        public List<TestAdaptiveSampler> Samplers { get; } = [];

        public IAdaptiveSampler Create(int samplesPerSecond)
        {
            RequestedRates.Add(samplesPerSecond);
            var sampler = new TestAdaptiveSampler();
            Samplers.Add(sampler);
            return sampler;
        }
    }

    private sealed class TestAdaptiveSampler : IAdaptiveSampler
    {
        private readonly Queue<bool> _results = new();

        public int DisposeCallCount { get; private set; }

        public int SampleCallCount { get; private set; }

        public void SetResults(params bool[] results)
        {
            _results.Clear();
            foreach (var result in results)
            {
                _results.Enqueue(result);
            }
        }

        public bool Sample()
        {
            SampleCallCount++;
            return _results.Count == 0 || _results.Dequeue();
        }

        public bool Keep() => true;

        public bool Drop() => false;

        public double NextDouble() => 0;

        public void Dispose()
        {
            DisposeCallCount++;
        }
    }
}
