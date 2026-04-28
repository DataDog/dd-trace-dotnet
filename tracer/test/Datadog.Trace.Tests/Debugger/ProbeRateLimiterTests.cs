// <copyright file="ProbeRateLimiterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Debugger.RateLimiting;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ProbeRateLimiterTests
{
    [Fact]
    public void ResetRate_DisposesRemovedSampler()
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

    private sealed class RecordingSamplerFactory
    {
        public List<TestAdaptiveSampler> Samplers { get; } = [];

        public IAdaptiveSampler Create(int samplesPerSecond)
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
}
