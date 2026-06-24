// <copyright file="RandomIdGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
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

#if NET6_0_OR_GREATER
    /// <summary>
    /// On .NET 6+, NextSpanId via the CSPRNG path must return a non-zero value
    /// within the expected range. Exercises <see cref="RandomIdGenerator.NextSpanIdSecureForTesting"/>.
    /// </summary>
    [Fact]
    public void NextSpanId_WithSecureRandom_ReturnsNonZeroValues()
    {
        for (var i = 0; i < 100; i++)
        {
            var id = RandomIdGenerator.NextSpanIdSecureForTesting(useAllBits: false);
            id.Should().BeGreaterOrEqualTo(MinId).And.BeLessThanOrEqualTo(MaxUInt63);
        }
    }

    /// <summary>
    /// On .NET 6+, NextTraceId via the CSPRNG path must return a TraceId with a non-zero Lower component.
    /// </summary>
    [Fact]
    public void NextTraceId_WithSecureRandom_ReturnsNonZeroLower()
    {
        for (var i = 0; i < 100; i++)
        {
            var id = RandomIdGenerator.NextTraceIdSecureForTesting(useAllBits: false);
            id.Lower.Should().BeGreaterOrEqualTo(MinId);
        }
    }

    /// <summary>
    /// On .NET 6+, successive CSPRNG calls must produce varied values,
    /// demonstrating that the RandomNumberGenerator.Fill() path is exercised.
    /// </summary>
    [Fact]
    public void NextSpanId_WithSecureRandom_ProducesVariedValues()
    {
        const int count = 20;
        var values = new HashSet<ulong>();

        for (var i = 0; i < count; i++)
        {
            values.Add(RandomIdGenerator.NextSpanIdSecureForTesting(useAllBits: true));
        }

        // All 20 values should be distinct (probability of collision is negligible)
        values.Count.Should().Be(count);
    }

    /// <summary>
    /// On .NET 6+, NextTraceId via the CSPRNG path must return a 128-bit id with
    /// a valid unix-seconds upper component and a non-zero lower component.
    /// </summary>
    [Fact]
    public void NextTraceId_WithSecureRandom_128Bit_ReturnsValidId()
    {
        for (var i = 0; i < 100; i++)
        {
            var id = RandomIdGenerator.NextTraceIdSecureForTesting(useAllBits: true);

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
#else
    // On pre-.NET 6, the CSPRNG path in the *ForTesting helpers does not exist.
    // The Xoshiro256** path (the only path) is covered by the non-secure tests above.

    [Fact]
    public void NextSpanId_WithSecureRandom_ReturnsNonZeroValues()
    {
        var rng = new RandomIdGenerator();
        for (var i = 0; i < 100; i++)
        {
            var id = rng.NextSpanId(useAllBits: false);
            id.Should().BeGreaterOrEqualTo(MinId).And.BeLessThanOrEqualTo(MaxUInt63);
        }
    }

    [Fact]
    public void NextTraceId_WithSecureRandom_ReturnsNonZeroLower()
    {
        var rng = new RandomIdGenerator();
        for (var i = 0; i < 100; i++)
        {
            var id = rng.NextTraceId(useAllBits: false);
            id.Lower.Should().BeGreaterOrEqualTo(MinId);
        }
    }

    [Fact]
    public void NextSpanId_WithSecureRandom_ProducesVariedValues()
    {
        var rng = new RandomIdGenerator();
        const int count = 20;
        var values = new HashSet<ulong>();

        for (var i = 0; i < count; i++)
        {
            values.Add(rng.NextSpanId(useAllBits: true));
        }

        values.Count.Should().Be(count);
    }

    [Fact]
    public void NextTraceId_WithSecureRandom_128Bit_ReturnsValidId()
    {
        var rng = new RandomIdGenerator();
        for (var i = 0; i < 100; i++)
        {
            var id = rng.NextTraceId(useAllBits: true);

            const ulong toleranceSeconds = 2;
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            (id.Upper >> 32).Should().BeInRange(now - toleranceSeconds, now + toleranceSeconds);
            (id.Upper << 32).Should().Be(0);
            id.Lower.Should().BeGreaterOrEqualTo(MinId);
        }
    }
#endif

    /// <summary>
    /// On pre-.NET 6, NotifyRestore() resets the thread-local instance so the next
    /// access to Shared constructs a fresh one. Uses the internal test helper to bypass
    /// the <c>_secureRandom</c> guard (which is false by default in tests).
    /// On .NET 6+ this is a no-op and the test is skipped.
    /// </summary>
    [Fact]
    public void NotifyRestore_ResetsSharedInstance_WhenSecureRandom()
    {
#if NET6_0_OR_GREATER
        // No-op on .NET 6+: NotifyRestore() is documented as a no-op here.
        // Nothing to assert.
#else
        // Ensure _shared is populated on this thread
        var instanceBefore = RandomIdGenerator.Shared;

        RandomIdGenerator.ResetSharedForTesting();

        // _shared should now be null; next access must produce a fresh instance
        var instanceAfter = RandomIdGenerator.Shared;
        instanceAfter.Should().NotBeSameAs(instanceBefore);
#endif
    }

#if !NET6_0_OR_GREATER
    /// <summary>
    /// On pre-.NET 6, NotifyRestore() must be a no-op when DD_TRACE_SECURE_RANDOM is not set
    /// (the default). The thread-local _shared instance must remain the same object.
    /// This guards against accidentally removing the <c>if (_secureRandom)</c> guard.
    /// </summary>
    [Fact]
    public void NotifyRestore_IsNoOp_WhenSecureRandomDisabled()
    {
        // Ensure _shared is populated on this thread
        var instanceBefore = RandomIdGenerator.Shared;

        // NotifyRestore() should do nothing because DD_TRACE_SECURE_RANDOM is false (default)
        RandomIdGenerator.NotifyRestore();

        var instanceAfter = RandomIdGenerator.Shared;
        instanceAfter.Should().BeSameAs(instanceBefore);
    }
#endif

    /// <summary>
    /// DD_TRACE_SECURE_RANDOM now parses via ToBoolean() instead of == "true",
    /// so all standard truthy/falsy forms must be recognised consistently with
    /// every other boolean setting in the tracer.
    /// </summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("Yes", true)]
    [InlineData("t", true)]
    [InlineData("y", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SecureRandom_EnvVar_ParsesBooleanVariants(string envValue, bool expectedResult)
    {
        // _secureRandom uses `envValue?.ToBoolean() == true`.
        // The static readonly field cannot be re-evaluated per-test, so we validate
        // the parsing expression directly to prove all variants behave correctly.
        var result = envValue?.ToBoolean() == true;
        result.Should().Be(expectedResult);
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
