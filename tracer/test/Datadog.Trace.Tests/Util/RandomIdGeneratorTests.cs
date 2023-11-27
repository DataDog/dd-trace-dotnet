// <copyright file="RandomIdGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class RandomIdGeneratorTests
{
    private const ulong MinId = 1;
    private const ulong MaxUInt63 = long.MaxValue;
    private const ulong MaxUInt64 = ulong.MaxValue;
    private const int NumberOfIdsToGenerate = 5_000_000;
    private const int NumberOfBuckets = 20;
    private const int ExpectedCountPerBucket = NumberOfIdsToGenerate / NumberOfBuckets;

    private readonly RandomIdGenerator _rng = new();

    [Fact]
    public void NextSpanId_63_Is_Valid()
    {
        var values = GetValues(() => _rng.NextSpanId(useAllBits: false));

        foreach (var value in values.Take(NumberOfIdsToGenerate))
        {
            // even though these are 64-bit ulong, they should never be larger than Int64.MaxValue
            value.Should().BeGreaterOrEqualTo(MinId).And.BeLessThanOrEqualTo(MaxUInt63);
        }
    }

    [Fact]
    public void NextSpanId_64_Is_Valid()
    {
        var values = GetValues(() => _rng.NextSpanId(useAllBits: true));

        foreach (var value in values.Take(NumberOfIdsToGenerate))
        {
            value.Should().BeGreaterOrEqualTo(MinId);
        }
    }

    [Fact]
    public void NextTraceId_63_Is_Valid()
    {
        // even though these are 128-bit TraceId, they should never be larger than Int64.MaxValue
        var values = GetValues(() => _rng.NextTraceId(useAllBits: false));

        foreach (var value in values.Take(NumberOfIdsToGenerate))
        {
            value.Upper.Should().Be(0);
            value.Lower.Should().BeGreaterOrEqualTo(MinId).And.BeLessThanOrEqualTo(MaxUInt63);
        }
    }

    [Fact]
    public void NextTraceId_128_Is_Valid()
    {
        // 128 bits = <32-bit unix seconds> <32 bits of zero> <64 random bits>
        var values = GetValues(() => _rng.NextTraceId(useAllBits: true));

        foreach (var value in values.Take(NumberOfIdsToGenerate))
        {
            const ulong timestampToleranceSeconds = 2;
            var unixTimeSeconds = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var lowestExpectedTimestamp = unixTimeSeconds - timestampToleranceSeconds;
            var highestExpectedTimestamp = unixTimeSeconds + timestampToleranceSeconds;

            // upper 32 bits are unix epoch seconds
            (value.Upper >> 32).Should().BeInRange(lowestExpectedTimestamp, highestExpectedTimestamp);

            // next 32 bits are always zero
            (value.Upper << 32).Should().Be(0);

            // lower 64 bits are never zero
            value.Lower.Should().BeGreaterOrEqualTo(MinId);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NextSpanId_Are_Not_Duplicated(bool useAllBits)
    {
        var values = GetValues(() => _rng.NextSpanId(useAllBits)).Take(NumberOfIdsToGenerate);
        var set = new HashSet<ulong>();

        foreach (var value in values)
        {
            // fail as soon as we see a duplicate
            set.Add(value).Should().BeTrue();
        }

        set.Count.Should().Be(NumberOfIdsToGenerate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NextTraceId_Are_Not_Duplicated(bool useAllBits)
    {
        // 128 bits = <32-bit unix seconds> <32 bits of zero> <64 random bits>
        // only the lower 64 bits are random, so use Trace.Lower and ignore TraceId.Upper
        var values = GetValues(() => _rng.NextTraceId(useAllBits).Lower).Take(NumberOfIdsToGenerate);
        var set = new HashSet<ulong>();

        foreach (var value in values)
        {
            // fail as soon as we see a duplicate
            set.Add(value).Should().BeTrue();
        }

        set.Count.Should().Be(NumberOfIdsToGenerate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NextSpanId_Are_Evenly_Distributed(bool useAllBits)
    {
        var values = GetValues(() => _rng.NextSpanId(useAllBits)).Take(NumberOfIdsToGenerate);

        AssertEvenDistribution(values, MinId, useAllBits ? MaxUInt64 : MaxUInt63);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NextTraceId_Lower_Are_Evenly_Distributed(bool useAllBits)
    {
        // 128 bits = <32-bit unix seconds> <32 bits of zero> <64 random bits>
        // only the lower 64 bits are random, so use Trace.Lower and ignore TraceId.Upper
        var values = GetValues(() => _rng.NextTraceId(useAllBits).Lower).Take(NumberOfIdsToGenerate);

        AssertEvenDistribution(values, MinId, useAllBits ? MaxUInt64 : MaxUInt63);
    }

    [Fact]
    public void Default_Is_128Bit_TraceId()
    {
        var tracer = new Tracer(
            new TracerSettings(),
            Mock.Of<IAgentWriter>(),
            Mock.Of<ITraceSampler>(),
            new AsyncLocalScopeManager(),
            Mock.Of<IDogStatsd>(),
            Mock.Of<ITelemetryController>(),
            Mock.Of<IDiscoveryService>());

        var scope = (Scope)tracer.StartActive("operation");

        scope.Span.TraceId128.Lower.Should().BeGreaterThan(0);
        scope.Span.TraceId128.Upper.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Configure_128Bit_TraceId_Disabled()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, false } });

        var tracer = new Tracer(
            settings,
            Mock.Of<IAgentWriter>(),
            Mock.Of<ITraceSampler>(),
            new AsyncLocalScopeManager(),
            Mock.Of<IDogStatsd>(),
            Mock.Of<ITelemetryController>(),
            Mock.Of<IDiscoveryService>());

        var scope = (Scope)tracer.StartActive("operation");

        scope.Span.TraceId128.Lower.Should().BeGreaterThan(0);
        scope.Span.TraceId128.Upper.Should().Be(0);
    }

    [Fact]
    public void Configure_128Bit_TraceId_Enabled()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, true } });

        var tracer = new Tracer(
            settings,
            Mock.Of<IAgentWriter>(),
            Mock.Of<ITraceSampler>(),
            new AsyncLocalScopeManager(),
            Mock.Of<IDogStatsd>(),
            Mock.Of<ITelemetryController>(),
            Mock.Of<IDiscoveryService>());

        var scope = (Scope)tracer.StartActive("operation");

        scope.Span.TraceId128.Lower.Should().BeGreaterThan(0);
        scope.Span.TraceId128.Upper.Should().BeGreaterThan(0);
    }

    private static void AssertEvenDistribution(IEnumerable<ulong> values, ulong minValue, ulong maxValue)
    {
        // quick and dirty check for even distribution by placing values into buckets and checking
        // that all bucket counts are within an acceptable threshold
        var bucketCounts = new int[NumberOfBuckets];

        foreach (var value in values)
        {
            var bucketIndex = (int)((double)NumberOfBuckets * (value - minValue) / maxValue);
            bucketCounts[bucketIndex] += 1;
        }

        // pre-compute expected lower and upper limits
        const double tolerancePercent = 0.01;
        const double lowestExpectedCount = ExpectedCountPerBucket * (1 - tolerancePercent);
        const double highestExpectedCount = ExpectedCountPerBucket * (1 + tolerancePercent);

        bucketCounts.Should().OnlyContain(count => lowestExpectedCount < count && count < highestExpectedCount);
    }

    private static IEnumerable<T> GetValues<T>(Func<T> factory)
    {
        while (true)
        {
            yield return factory();
        }
    }
}
