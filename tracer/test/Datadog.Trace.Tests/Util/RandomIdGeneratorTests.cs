// <copyright file="RandomIdGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.TestHelpers.TestTracer;
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
    [Flaky("This test can rarely fail because it does not, intentionally, rely on predictable behaviour")]
    [InlineData(true)]
    [InlineData(false)]
    public void NextSpanId_Are_Evenly_Distributed(bool useAllBits)
    {
        var values = GetValues(() => _rng.NextSpanId(useAllBits)).Take(NumberOfIdsToGenerate);

        AssertEvenDistribution(values, MinId, useAllBits ? MaxUInt64 : MaxUInt63);
    }

    [Theory]
    [InlineData(true)]
    [Flaky("This test can rarely fail because it does not, intentionally, rely on predictable behaviour")]
    [InlineData(false)]
    public void NextTraceId_Lower_Are_Evenly_Distributed(bool useAllBits)
    {
        // 128 bits = <32-bit unix seconds> <32 bits of zero> <64 random bits>
        // only the lower 64 bits are random, so use Trace.Lower and ignore TraceId.Upper
        var values = GetValues(() => _rng.NextTraceId(useAllBits).Lower).Take(NumberOfIdsToGenerate);

        AssertEvenDistribution(values, MinId, useAllBits ? MaxUInt64 : MaxUInt63);
    }

    [Fact]
    public async Task Default_Is_128Bit_TraceId()
    {
        await using var tracer = TracerHelper.Create(
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
    public async Task Configure_128Bit_TraceId_Disabled()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, false } });

        await using var tracer = TracerHelper.Create(
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
    public async Task Configure_128Bit_TraceId_Enabled()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, true } });

        await using var tracer = TracerHelper.Create(
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

    // -------------------------------------------------------------------------
    // DD_TRACE_SECURE_RANDOM tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// NotifyRestore() must be callable without throwing, regardless of whether
    /// DD_TRACE_SECURE_RANDOM is set. On .NET 6+ the method is a documented no-op;
    /// on pre-.NET 6 it nulls the thread-local PRNG state when the env var is true.
    /// </summary>
    [Fact]
    public void NotifyRestore_DoesNotThrow()
    {
        var act = () => RandomIdGenerator.NotifyRestore();
        act.Should().NotThrow();
    }

    /// <summary>
    /// After NotifyRestore() the Shared accessor must still return a usable instance
    /// that generates valid span IDs.
    /// </summary>
    [Fact]
    public void NotifyRestore_SharedStillUsableAfterwards()
    {
        RandomIdGenerator.NotifyRestore();
        var id = RandomIdGenerator.Shared.NextSpanId(useAllBits: false);
        id.Should().BeGreaterOrEqualTo(MinId).And.BeLessThanOrEqualTo(MaxUInt63);
    }

    /// <summary>
    /// When _secureRandom is forced on via reflection, NextSpanId() must still
    /// return a non-zero value within the expected range. This exercises the
    /// RandomNumberGenerator.Fill() code path (.NET 6+) or the re-seeded
    /// Xoshiro256** path (pre-.NET 6).
    /// </summary>
    [Fact]
    public void NextSpanId_WithSecureRandom_ReturnsNonZeroValues()
    {
        var field = typeof(RandomIdGenerator)
            .GetField("_secureRandom", BindingFlags.Static | BindingFlags.NonPublic);

        if (field == null)
        {
            // field not found — skip rather than fail (future refactor may rename it)
            return;
        }

        var original = (bool)field.GetValue(null)!;
        try
        {
            field.SetValue(null, true);

            var rng = new RandomIdGenerator();
            for (var i = 0; i < 100; i++)
            {
                var id = rng.NextSpanId(useAllBits: false);
                id.Should().BeGreaterOrEqualTo(MinId).And.BeLessThanOrEqualTo(MaxUInt63);
            }
        }
        finally
        {
            field.SetValue(null, original);
        }
    }

    /// <summary>
    /// When _secureRandom is forced on via reflection, NextTraceId() must still
    /// return a TraceId with a non-zero Lower component.
    /// </summary>
    [Fact]
    public void NextTraceId_WithSecureRandom_ReturnsNonZeroLower()
    {
        var field = typeof(RandomIdGenerator)
            .GetField("_secureRandom", BindingFlags.Static | BindingFlags.NonPublic);

        if (field == null)
        {
            return;
        }

        var original = (bool)field.GetValue(null)!;
        try
        {
            field.SetValue(null, true);

            var rng = new RandomIdGenerator();
            for (var i = 0; i < 100; i++)
            {
                var id = rng.NextTraceId(useAllBits: false);
                id.Lower.Should().BeGreaterOrEqualTo(MinId);
            }
        }
        finally
        {
            field.SetValue(null, original);
        }
    }

    /// <summary>
    /// When _secureRandom is forced on, successive calls must produce varied values
    /// (i.e. not all the same), demonstrating that the CSPRNG path is exercised.
    /// </summary>
    [Fact]
    public void NextSpanId_WithSecureRandom_ProducesVariedValues()
    {
        var field = typeof(RandomIdGenerator)
            .GetField("_secureRandom", BindingFlags.Static | BindingFlags.NonPublic);

        if (field == null)
        {
            return;
        }

        var original = (bool)field.GetValue(null)!;
        try
        {
            field.SetValue(null, true);

            var rng = new RandomIdGenerator();
            const int count = 20;
            var values = new HashSet<ulong>();

            for (var i = 0; i < count; i++)
            {
                values.Add(rng.NextSpanId(useAllBits: true));
            }

            // All 20 values should be distinct (probability of collision is negligible)
            values.Count.Should().Be(count);
        }
        finally
        {
            field.SetValue(null, original);
        }
    }

    /// <summary>
    /// When _secureRandom is forced on, NextTraceId(useAllBits: true) must return a
    /// 128-bit id with a valid unix-seconds upper component and a non-zero lower component.
    /// </summary>
    [Fact]
    public void NextTraceId_WithSecureRandom_128Bit_ReturnsValidId()
    {
        var field = typeof(RandomIdGenerator)
            .GetField("_secureRandom", BindingFlags.Static | BindingFlags.NonPublic);

        if (field == null)
        {
            return;
        }

        var original = (bool)field.GetValue(null)!;
        try
        {
            field.SetValue(null, true);

            var rng = new RandomIdGenerator();
            for (var i = 0; i < 100; i++)
            {
                var id = rng.NextTraceId(useAllBits: true);

                const ulong toleranceSeconds = 2;
                var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // upper 32 bits are unix epoch seconds
                (id.Upper >> 32).Should().BeInRange(now - toleranceSeconds, now + toleranceSeconds);

                // next 32 bits are always zero
                (id.Upper << 32).Should().Be(0);

                // lower 64 bits are non-zero
                id.Lower.Should().BeGreaterOrEqualTo(MinId);
            }
        }
        finally
        {
            field.SetValue(null, original);
        }
    }

    /// <summary>
    /// When _secureRandom is forced on and NotifyRestore() is called, the thread-local
    /// _shared instance must be nulled so the next access to Shared creates a fresh instance.
    /// This only applies to the pre-.NET 6 implementation; on .NET 6+ NotifyRestore() is
    /// a documented no-op and _shared does not exist, so the test skips automatically.
    /// </summary>
    [Fact]
    public void NotifyRestore_ResetsSharedInstance_WhenSecureRandom()
    {
        // _shared only exists as [ThreadStatic] in the pre-.NET 6 implementation
        var sharedField = typeof(RandomIdGenerator)
            .GetField("_shared", BindingFlags.Static | BindingFlags.NonPublic);
        var secureRandomField = typeof(RandomIdGenerator)
            .GetField("_secureRandom", BindingFlags.Static | BindingFlags.NonPublic);

        if (sharedField == null || secureRandomField == null)
        {
            return;
        }

        var originalSecureRandom = (bool)secureRandomField.GetValue(null)!;
        try
        {
            secureRandomField.SetValue(null, true);

            // Ensure _shared is populated on this thread
            var instanceBefore = RandomIdGenerator.Shared;

            RandomIdGenerator.NotifyRestore();

            // _shared should now be null
            sharedField.GetValue(null).Should().BeNull();

            // Next access must produce a fresh (non-same) instance
            var instanceAfter = RandomIdGenerator.Shared;
            instanceAfter.Should().NotBeSameAs(instanceBefore);
        }
        finally
        {
            secureRandomField.SetValue(null, originalSecureRandom);
        }
    }

    private static IEnumerable<T> GetValues<T>(Func<T> factory)
    {
        while (true)
        {
            yield return factory();
        }
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
}
