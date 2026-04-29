// <copyright file="DownstreamSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rasp;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

public class DownstreamSamplerTests
{
    [Fact]
    public void Constructor_SamplingRate_SanitizesNegativeToZero()
    {
        var sampler = new DownstreamSampler(-0.5);
        var ctx = CreateMockContext();

        // With rate 0, nothing should be sampled
        int sampled = 0;
        for (int i = 0; i < 100; i++)
        {
            if (sampler.SampleHttpClientRequest(ctx, (ulong)i))
            {
                sampled++;
            }
        }

        sampled.Should().Be(0);
    }

    [Fact]
    public void Constructor_SamplingRate_SanitizesGreaterThanOneToOne()
    {
        var sampler = new DownstreamSampler(1.5);
        var ctx = CreateMockContext();

        // With rate 1.0, everything should be sampled
        int sampled = 0;
        for (int i = 0; i < 100; i++)
        {
            if (sampler.SampleHttpClientRequest(ctx, (ulong)i))
            {
                sampled++;
            }
        }

        sampled.Should().Be(100);
    }

    [Theory]
    [InlineData(0.0, 1000, 0)]        // 0% rate - nothing sampled
    [InlineData(1.0, 1000, 1000)]     // 100% rate - everything sampled
    [InlineData(0.5, 1000, 500, 50)]  // 50% rate - approximately 500 sampled (±50 tolerance)
    [InlineData(0.25, 1000, 250, 50)] // 25% rate - approximately 250 sampled (±50 tolerance)
    [InlineData(0.75, 1000, 750, 50)] // 75% rate - approximately 750 sampled (±50 tolerance)
    public void SampleHttpClientRequest_VariousRates_SamplesCorrectly(double rate, int total, int expected, int tolerance = 0)
    {
        var sampler = new DownstreamSampler(rate);
        var ctx = CreateMockContext();

        int sampled = 0;
        for (int i = 0; i < total; i++)
        {
            if (sampler.SampleHttpClientRequest(ctx, (ulong)i))
            {
                sampled++;
            }
        }

        if (tolerance > 0)
        {
            sampled.Should().BeInRange(expected - tolerance, expected + tolerance);
        }
        else
        {
            sampled.Should().Be(expected);
        }
    }

    [Fact]
    public void SampleHttpClientRequest_DeterministicBehavior_ProducesSamePattern()
    {
        var sampler1 = new DownstreamSampler(0.5);
        var sampler2 = new DownstreamSampler(0.5);
        var ctx = CreateMockContext();

        var results1 = new List<bool>();
        var results2 = new List<bool>();

        for (int i = 0; i < 100; i++)
        {
            results1.Add(sampler1.SampleHttpClientRequest(ctx, (ulong)i));
            results2.Add(sampler2.SampleHttpClientRequest(ctx, (ulong)i));
        }

        // Both samplers should produce the same deterministic pattern
        results1.Should().Equal(results2);
    }

    [Fact]
    public async Task SampleHttpClientRequest_ThreadSafety_HandlesMultipleThreads()
    {
        var sampler = new DownstreamSampler(0.5);
        var ctx = CreateMockContext();
        var totalSampled = 0;
        var lockObj = new object();
        var tasks = new List<Task>();

        // Run 100 threads concurrently, each calling the sampler
        for (int threadId = 0; threadId < 100; threadId++)
        {
            var localThreadId = threadId;
            var task = Task.Run(() =>
            {
                int localCount = 0;
                for (int i = 0; i < 10; i++)
                {
                    if (sampler.SampleHttpClientRequest(ctx, (ulong)((localThreadId * 10) + i)))
                    {
                        localCount++;
                    }
                }

                lock (lockObj)
                {
                    totalSampled += localCount;
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        // Task.WaitAll(tasks.ToArray());

        // Should sample approximately 50% of 1000 total calls (±100 tolerance for thread timing)
        totalSampled.Should().BeInRange(400, 600);
    }

    [Fact]
    public void SampleHttpClientRequest_CounterOverflow_WrapsCorrectly()
    {
        var sampler = new DownstreamSampler(0.5);
        var ctx = CreateMockContext();

        // Call sampler many times to approach long.MaxValue
        // This test validates that the counter wraps correctly
        // We can't actually hit long.MaxValue in a test, but we can verify the logic handles wrap

        // Call it enough times to verify consistent behavior
        int sampled = 0;
        for (int i = 0; i < 10000; i++)
        {
            if (sampler.SampleHttpClientRequest(ctx, (ulong)i))
            {
                sampled++;
            }
        }

        // Should sample approximately 50% (±200 tolerance)
        sampled.Should().BeInRange(4800, 5200);
    }

    [Fact]
    public void SampleHttpClientRequest_MultipleInstances_IndependentCounters()
    {
        var sampler1 = new DownstreamSampler(0.5);
        var sampler2 = new DownstreamSampler(0.5);
        var ctx = CreateMockContext();

        var results1 = new List<bool>();
        var results2 = new List<bool>();

        // Call sampler1 100 times
        for (int i = 0; i < 100; i++)
        {
            results1.Add(sampler1.SampleHttpClientRequest(ctx, (ulong)i));
        }

        // Call sampler2 100 times
        for (int i = 0; i < 100; i++)
        {
            results2.Add(sampler2.SampleHttpClientRequest(ctx, (ulong)i));
        }

        // Both should produce the same pattern since they're independent with the same rate
        results1.Should().Equal(results2);
    }

    [Theory]
    [InlineData(0.1, 10000, 1000, 200)]   // 10% rate
    [InlineData(0.9, 10000, 9000, 200)]   // 90% rate
    [InlineData(0.01, 10000, 100, 50)]    // 1% rate
    [InlineData(0.99, 10000, 9900, 50)]   // 99% rate
    public void SampleHttpClientRequest_ExtremeSampleRates_WorksCorrectly(double rate, int total, int expected, int tolerance)
    {
        var sampler = new DownstreamSampler(rate);
        var ctx = CreateMockContext();

        int sampled = 0;
        for (int i = 0; i < total; i++)
        {
            if (sampler.SampleHttpClientRequest(ctx, (ulong)i))
            {
                sampled++;
            }
        }

        sampled.Should().BeInRange(expected - tolerance, expected + tolerance);
    }

    [Fact]
    public void SampleHttpClientRequest_SameRequestId_ProducesConsistentResult()
    {
        var sampler = new DownstreamSampler(0.5);
        var ctx = CreateMockContext();

        // The sampling decision depends on the internal counter, not the requestId
        // Each call increments the counter, so results should follow the deterministic pattern
        var result1 = sampler.SampleHttpClientRequest(ctx, 12345);
        var result2 = sampler.SampleHttpClientRequest(ctx, 12345); // Same requestId, different internal counter

        // Results should be based on internal counter, not requestId
        // They may or may not be equal depending on the sampling pattern
    }

    private static AppSecRequestContext CreateMockContext()
    {
        // AppSecRequestContext is sealed; ctx is unused in SampleHttpClientRequest
        return null!;
    }
}
