// <copyright file="RandomIdGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Util;
using FluentAssertions;
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

    [Fact]
    public void NextSpanId_UInt63_Are_Valid()
    {
        // even though these are ulong, they should never be larger than long.MaxValue
        GetValues(() => RandomIdGenerator.Shared.NextSpanId(useUInt64MaxValue: false))
           .Take(NumberOfIdsToGenerate)
           .Should()
           .OnlyContain(i => i >= MinId && i <= MaxUInt63);
    }

    [Fact]
    public void NextSpanId_UInt64_Are_Valid()
    {
        GetValues(() => RandomIdGenerator.Shared.NextSpanId(useUInt64MaxValue: true))
           .Take(NumberOfIdsToGenerate)
           .Should()
           .OnlyContain(i => i >= MinId);
    }

    [Fact]
    public void NextTraceId_UInt128_Are_Valid()
    {
        GetValues(() => RandomIdGenerator.Shared.NextTraceId())
           .Take(NumberOfIdsToGenerate)
           .Should()
           .OnlyContain(i => i >= MinId);
    }

    [Fact]
    public void NextSpanId_UInt63_Are_Not_Duplicated()
    {
        var values = GetValues(() => RandomIdGenerator.Shared.NextSpanId(useUInt64MaxValue: false)).Take(NumberOfIdsToGenerate);
        var set = new HashSet<ulong>(values);

        set.Count.Should().Be(NumberOfIdsToGenerate);
    }

    [Fact]
    public void NextSpanId_UInt64_Are_Not_Duplicated()
    {
        var values = GetValues(() => RandomIdGenerator.Shared.NextSpanId(useUInt64MaxValue: true)).Take(NumberOfIdsToGenerate);
        var set = new HashSet<ulong>(values);

        set.Count.Should().Be(NumberOfIdsToGenerate);
    }

    [Fact]
    public void NextTraceId_UInt128_Are_Not_Duplicated()
    {
        // only the lower 64 bits are random, so use Trace.Lower and ignore TraceId.Upper
        var values = GetValues(() => RandomIdGenerator.Shared.NextTraceId().Lower).Take(NumberOfIdsToGenerate);
        var set = new HashSet<ulong>(values);

        set.Count.Should().Be(NumberOfIdsToGenerate);
    }

    [Fact]
    public void NextSpanId_UInt63_Are_Evenly_Distributed()
    {
        var values = GetValues(() => RandomIdGenerator.Shared.NextSpanId(useUInt64MaxValue: false)).Take(NumberOfIdsToGenerate);
        AssertEvenDistribution(values, MinId, MaxUInt63);
    }

    [Fact]
    public void NextSpanId_UInt64_Are_Evenly_Distributed()
    {
        var values = GetValues(() => RandomIdGenerator.Shared.NextSpanId(useUInt64MaxValue: true)).Take(NumberOfIdsToGenerate);
        AssertEvenDistribution(values, MinId, MaxUInt64);
    }

    [Fact]
    public void NextTraceId_Lower_Are_Evenly_Distributed()
    {
        // only the lower 64 bits are random, so use Trace.Lower and ignore TraceId.Upper
        var values = GetValues(() => RandomIdGenerator.Shared.NextTraceId().Lower).Take(NumberOfIdsToGenerate);
        AssertEvenDistribution(values, MinId, MaxUInt64);
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

        bucketCounts.Should().OnlyContain(count => (double)Math.Abs(ExpectedCountPerBucket - count) / ExpectedCountPerBucket < 0.01);
    }

    private static IEnumerable<T> GetValues<T>(Func<T> factory)
    {
        while (true)
        {
            yield return factory();
        }
    }
}
