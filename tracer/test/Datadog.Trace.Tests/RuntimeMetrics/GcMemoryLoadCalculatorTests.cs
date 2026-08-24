// <copyright file="GcMemoryLoadCalculatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(PlatformKeys.DotNetGCHighMemPercent, PlatformKeys.ComPlusGCHighMemPercent)]
public class GcMemoryLoadCalculatorTests
{
    private const long Total8GiB = 8L * 1024 * 1024 * 1024;
    private const long Total96GiB = 96L * 1024 * 1024 * 1024;

    [Fact]
    public void TryCalculate_NoHardLimit_RecoversTrueLoad()
    {
        var highMemoryLoadThresholdBytes = AsBytes(Total8GiB, 90);
        var totalAvailableMemoryBytes = Total8GiB;
        var memoryLoadBytes = AsBytes(Total8GiB, 42);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, hasConfiguredHighMemoryLoadPercent: false);

        result.Should().Be(42);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(42)]
    [InlineData(80)]
    [InlineData(99)]
    [InlineData(100)]
    public void TryCalculate_DefaultContainerHardLimit_RecoversTrueLoad(int loadPercent)
    {
        // e.g. a memory-limited container with no explicit GC hard limit, where the runtime defaults to 75%
        var highMemoryLoadThresholdBytes = AsBytes(Total8GiB, 90);
        var totalAvailableMemoryBytes = AsBytes(Total8GiB, 75);
        var memoryLoadBytes = AsBytes(Total8GiB, loadPercent);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, hasConfiguredHighMemoryLoadPercent: false);

        result.Should().Be(loadPercent);
    }

    [Theory]
    // An explicit threshold-percent override (DOTNET_GCHighMemPercent / System.GC.HighMemoryPercent) can't be
    // reliably inverted without parsing those values identically to the runtime, which is fragile.
    [InlineData(42, 70, 75, true, 56)]
    // >= 80GiB, no hard limit: fallback
    [InlineData(42_000_000_000, 97_000_000_000, 100_000_000_000, false, 42)]
    // TotalAvailableMemoryBytes (heap_hard_limit) can never exceed total_physical_mem, so a tiny threshold next to
    // a huge totalAvailableMemoryBytes can never be consistent with that invariant.
    [InlineData(500, 1000, 2000, false, 25)]
    // Like the primary calculation, the fallback clamps to [0, 100] - it must not publish a value above 100 just
    // because the actual load already exceeds TotalAvailableMemoryBytes...
    [InlineData(90, 70, 75, true, 100)]
    // ...nor below 0 for a negative load.
    [InlineData(-1000, 70, 75, true, 0)]
    public void TryCalculate_WhenItCannotInvertTheThreshold_FallsBackToLoadRelativeToTotalAvailableMemory(
        long memoryLoadBytes, long highMemoryLoadThresholdBytes, long totalAvailableMemoryBytes, bool hasConfiguredHighMemoryLoadPercent, double expected)
    {
        // We can't reliably recover the *true* load percentage relative to the threshold in these cases - so
        // instead of bailing out, this falls back to the simple pre-refactor calculation of load relative to
        // TotalAvailableMemoryBytes.
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, hasConfiguredHighMemoryLoadPercent);

        result.Should().Be(expected);
    }

    [Fact]
    public void TryCalculate_HighMemoryLoadThresholdBytesIsZero_ReturnsNull()
    {
        // HighMemoryLoadThresholdBytes is 0 before the first GC has run
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 500, highMemoryLoadThresholdBytes: 0, totalAvailableMemoryBytes: Total8GiB, hasConfiguredHighMemoryLoadPercent: false);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCalculate_TotalAvailableMemoryBytesIsZero_ReturnsNull()
    {
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 500, highMemoryLoadThresholdBytes: 1000, totalAvailableMemoryBytes: 0, hasConfiguredHighMemoryLoadPercent: false);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCalculate_ClampsResultToUpperBound()
    {
        // Defensive clamp: this shouldn't happen for real GC-reported values, but we must never publish
        // something outside the 0-100 range.
        var highMemoryLoadThresholdBytes = AsBytes(Total8GiB, 90);
        var totalAvailableMemoryBytes = Total8GiB;
        var memoryLoadBytes = AsBytes(Total8GiB, 150);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, hasConfiguredHighMemoryLoadPercent: false);

        result.Should().Be(100);
    }

    [Fact]
    public void TryCalculate_ClampsResultToLowerBound()
    {
        var highMemoryLoadThresholdBytes = AsBytes(Total8GiB, 90);
        var totalAvailableMemoryBytes = Total8GiB;

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: -1000, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, hasConfiguredHighMemoryLoadPercent: false);

        result.Should().Be(0);
    }

    [Fact]
    public void TryGetMemoryLoadPercentage_LiveGcMemoryInfo_ReturnsValueInRangeOrNull()
    {
        var info = GC.GetGCMemoryInfo();

        var result = GcMemoryLoadCalculator.TryGetMemoryLoadPercentage(info);

        // Legitimately null if no GC has run yet, a configured override is in play, or the host is >= 80GiB -
        // but whenever it does resolve, it must be a valid percentage.
        if (result is { } value)
        {
            value.Should().BeInRange(0, 100);
        }
    }

    // AppContext.SetData is public at runtime on every supported TFM, but the net6.0 (and earlier) reference
    // assemblies used to compile this project don't declare it - only GetData, which the product code under test
    // relies on - so a direct call fails to compile for those TargetFrameworks specifically.
#if NET7_0_OR_GREATER
    [Fact]
    public void ReadHasConfiguredHighMemoryLoadPercent_NoConfig_ReturnsFalse()
    {
        ClearAppContextData();

        var result = GcMemoryLoadCalculator.ReadHasConfiguredHighMemoryLoadPercent();

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(PlatformKeys.DotNetGCHighMemPercent, "50")]
    [InlineData(PlatformKeys.ComPlusGCHighMemPercent, "50")]
    [InlineData(PlatformKeys.DotNetGCHighMemPercent, "0")]
    [InlineData(PlatformKeys.ComPlusGCHighMemPercent, "0")]
    [InlineData(PlatformKeys.DotNetGCHighMemPercent, " ")]
    [InlineData(PlatformKeys.ComPlusGCHighMemPercent, " ")]
    // Below net9.0, Environment.SetEnvironmentVariable(name, "") deletes the variable instead of
    // setting it to an empty value, on every platform .NET runs on (not just Windows), so we can't
    // construct a "present but empty" env var there. net9.0 changed this: empty strings
    // are now persisted everywhere - so on net9.0+ this case runs for real.
#if NET9_0_OR_GREATER
    [InlineData(PlatformKeys.DotNetGCHighMemPercent, "")]
    [InlineData(PlatformKeys.ComPlusGCHighMemPercent, "")]
#endif
    public void ReadHasConfiguredHighMemoryLoadPercent_ComPlusEnvVarSet_ReturnsTrue(string envVar, string value)
    {
        ClearAppContextData();
        Environment.SetEnvironmentVariable(envVar, value);

        var result = GcMemoryLoadCalculator.ReadHasConfiguredHighMemoryLoadPercent();

        result.Should().BeTrue();
    }

    [Fact]
    public void ReadHasConfiguredHighMemoryLoadPercent_RuntimeConfigKnobSetAsString_ReturnsTrue()
    {
        ClearAppContextData();

        try
        {
            AppContext.SetData("System.GC.HighMemoryPercent", "70");

            var result = GcMemoryLoadCalculator.ReadHasConfiguredHighMemoryLoadPercent();

            result.Should().BeTrue();
        }
        finally
        {
            ClearAppContextData();
        }
    }

    [Fact]
    public void ReadHasConfiguredHighMemoryLoadPercent_RuntimeConfigKnobSetAsNonString_ReturnsFalse()
    {
        // runtimeconfig properties always reach AppContext as strings - anything else was set by user code
        // after startup, so the GC will never see it
        ClearAppContextData();

        try
        {
            AppContext.SetData("System.GC.HighMemoryPercent", 70);

            var result = GcMemoryLoadCalculator.ReadHasConfiguredHighMemoryLoadPercent();

            result.Should().BeFalse();
        }
        finally
        {
            ClearAppContextData();
        }
    }
#endif

    // Mirrors how the GC encodes a percentage into bytes (compute_memory_settings, src/coreclr/gc/init.cpp as of
    // writing - see the pinned source reference on GcMemoryLoadCalculator.EightyGiBBytesAt90Percent):
    // `(uint64_t)(pct / 100 * total_physical_mem)`. Each test builds inputs the same way the runtime would, then
    // asserts we decode the original percentage back.
    private static long AsBytes(long total, double percent) => (long)((percent / 100.0) * total);

    // AppContext.SetData is public at runtime on every supported TFM, but the net6.0 (and earlier) reference
    // assemblies used to compile this project don't declare it. Below net7.0, nothing in this test binary can
    // set the value in the first place, so clearing it is a no-op.
#if NET7_0_OR_GREATER
    private static void ClearAppContextData() => AppContext.SetData("System.GC.HighMemoryPercent", null);
#endif
}
#endif
