// <copyright file="GcMemoryLoadCalculatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using Datadog.Trace.RuntimeMetrics;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics;

public class GcMemoryLoadCalculatorTests
{
    private const long Total8GiB = 8L * 1024 * 1024 * 1024;
    private const long Total79GiB = 79L * 1024 * 1024 * 1024;
    private const long Total96GiB = 96L * 1024 * 1024 * 1024;
    private static readonly Func<int?> GetTotalProcessorCount = () => 4;

    [Fact]
    public void Calculate_NoHardLimit_RecoversTrueLoad()
    {
        var highMemoryLoadThresholdBytes = Encode(Total8GiB, 90);
        var totalAvailableMemoryBytes = Total8GiB;
        var memoryLoadBytes = Encode(Total8GiB, 42);

        var result = GcMemoryLoadCalculator.TryCalculate(
            memoryLoadBytes,
            highMemoryLoadThresholdBytes,
            totalAvailableMemoryBytes,
            configuredHighPercent: null,
            GetTotalProcessorCount);

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
    public void Calculate_DefaultContainerHardLimit_RecoversTrueLoad(int loadPercent)
    {
        var highMemoryLoadThresholdBytes = Encode(Total8GiB, 90);
        var totalAvailableMemoryBytes = Encode(Total8GiB, 75);
        var memoryLoadBytes = Encode(Total8GiB, loadPercent);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, configuredHighPercent: null, GetTotalProcessorCount);

        result.Should().Be(loadPercent);
    }

    [Fact]
    public void Calculate_ConfiguredViaHexEnvVar_ClampsToNinetyNine()
    {
        // "90" parsed as hex is 144, which the runtime clamps to 99
        var configuredHighPercent = GcMemoryLoadCalculator.ParseEnvHighMemPercent("90".AsSpan());
        configuredHighPercent.Should().Be(99);

        var highMemoryLoadThresholdBytes = Encode(Total8GiB, 99);
        var totalAvailableMemoryBytes = Encode(Total8GiB, 50);
        var memoryLoadBytes = Encode(Total8GiB, 42);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, configuredHighPercent, GetTotalProcessorCount);

        result.Should().Be(42);
    }

    [Fact]
    public void Calculate_ConfiguredViaRuntimeConfigKnob_ParsesDecimal()
    {
        // The runtimeconfig knob (System.GC.HighMemoryPercent) is only consulted if the environment variable
        // is unset (see ParseEnvHighMemPercent_ResolvesExpectedValue / ReadConfiguredHighMemoryLoadPercent for
        // that precedence), and is parsed as decimal.
        var configuredHighPercent = GcMemoryLoadCalculator.ParseAppContextHighMemPercent((object?)"95");
        configuredHighPercent.Should().Be(95);

        var highMemoryLoadThresholdBytes = Encode(Total8GiB, 95);
        var totalAvailableMemoryBytes = Encode(Total8GiB, 50);
        var memoryLoadBytes = Encode(Total8GiB, 42);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, configuredHighPercent, GetTotalProcessorCount);

        result.Should().Be(42);
    }

    [Theory]
    [InlineData("0", null)]
    [InlineData("1", 1)]
    [InlineData("5", 5)]
    [InlineData("9", 9)]
    [InlineData("a", 10)]
    [InlineData("A", 10)]
    [InlineData("007", 7)]
    [InlineData("50", 80)]
    [InlineData("63", 99)]
    [InlineData("64", 99)]
    [InlineData("99", 99)]
    [InlineData("ff", 99)]
    [InlineData("FF", 99)]
    [InlineData("0x50", 80)]
    [InlineData("0X50", 80)]
    [InlineData("0x1", 1)]
    [InlineData("0x63", 99)]
    [InlineData("0xFF", 99)]
    [InlineData("0x0", null)]
    [InlineData(" 50", 80)]
    [InlineData("50 ", 80)]
    [InlineData(" 63 ", 99)]
    [InlineData("50xyz", 80)]
    [InlineData("50.5", 80)]
    [InlineData("5g", 5)]
    [InlineData("63!!", 99)]
    [InlineData("1,2,3", 1)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("xyz", null)]
    [InlineData("gg", null)]
    [InlineData("-", null)]
    [InlineData("+", null)]
    [InlineData("0x", null)]
    [InlineData("+50", 80)]
    [InlineData("-1", 99)] // the real runtime accepts the sign, wraps to a huge unsigned value, and clamps to 99
    [InlineData("-5", 99)]
    [InlineData("-50", 99)]
    [InlineData("-63", 99)]
    [InlineData("-64", 99)]
    [InlineData("-0", null)]
    [InlineData("100000000", null)] // 0x100000000 is exactly 2^32, so the (uint32_t) truncation in init.cpp zeroes it out entirely; a huge configured value silently becomes "unconfigured."
    [InlineData("100000005", 5)] // truncation isn't just "big values become 99"; it's a literal low-32-bit truncation
    [InlineData("100000063", 99)]
    [InlineData("-100000000", null)]
    [InlineData("10000000000000000", null)] // 17+ hex digits overflow 64-bit unsigned, strtoull sets ERANGE, and GetIntConfigValue treats that as a hard failure (unconfigured), not a clamp.
    [InlineData("1ffffffffffffffff", null)]
    [InlineData("-1ffffffffffffffff", null)] // ERANGE is a hard failure regardless of sign on the env path - unlike the runtimeconfig path below, it never reaches the sign/clamp logic at all
    [InlineData("ffffffffffffffff", 99)] // exactly UINT64_MAX, fits in 64 bits with no ERANGE, so it takes the normal wraparound-then-clamp path
    public void ParseEnvHighMemPercent_ResolvesExpectedValue(string? envValue, int? expected)
    {
        var result = GcMemoryLoadCalculator.ParseEnvHighMemPercent(envValue.AsSpan());
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("50", 50)]
    [InlineData("63", 63)]
    [InlineData("90", 90)]
    [InlineData("98", 98)]
    [InlineData("99", 99)]
    [InlineData("100", 99)]
    [InlineData("153", 99)]
    [InlineData("0", null)]
    [InlineData("0x50", 80)]
    [InlineData("0X1E", 30)]
    [InlineData("0x63", 99)]
    [InlineData("0xFF", 99)]
    [InlineData("0x0", null)]
    [InlineData("010", 8)]
    [InlineData("045", 37)]
    [InlineData("077", 63)]
    [InlineData("090", null)]
    [InlineData("099", null)]
    [InlineData("008", null)]
    [InlineData("08", null)]
    [InlineData("-1", 99)]
    [InlineData("-50", 99)]
    [InlineData("+50", 50)]
    [InlineData("-0", null)]
    [InlineData("-0x50", 99)]
    [InlineData(" 50", 50)]
    [InlineData("50 ", 50)]
    [InlineData("50abc", 50)]
    [InlineData("50.5", 50)]
    [InlineData("63,64", 63)]
    [InlineData("true", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("abc", null)]
    [InlineData("-", null)]
    [InlineData("+", null)]
    [InlineData("0x", null)]
    [InlineData("ffffffffffffffff", null)]
    [InlineData("99999999999999999999", 99)]
    [InlineData("18446744073709551616", 99)]
    [InlineData("4294967296", null)]
    [InlineData("4294967301", 5)]
    [InlineData("4294967395", 99)]
    [InlineData("-4294967296", null)]
    [InlineData("-010", 99)]
    [InlineData("-18446744073709551616", 99)] // magnitude is exactly 2^64: overflows before the sign is applied, so strtoull saturates to ULLONG_MAX (not negated down to 1) and that clamps to 99
    [InlineData("-99999999999999999999", 99)] // same, with a magnitude far past 2^64
    [InlineData("-0xffffffffffffffffff", 99)] // same, via the hex prefix instead of decimal
    public void ParseAppContextHighMemPercent_ResolvesExpectedValue(object? appContextValue, int? expected)
    {
        var result = GcMemoryLoadCalculator.ParseAppContextHighMemPercent(appContextValue);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(96, 97, 48, 97)] // min(10, 3 + (int)(47 / 48)) == 3 -> th == 97
    [InlineData(82, 90, 4, 90)] // min(10, 3 + (int)(47 / 4)) == min(10, 14) == 10 -> th == 90
    [InlineData(96, 97, null, null)] // needs the host-wide processor count; if it couldn't be reliably determined, bail out
    public void ResolveHighMemoryLoadThresholdPercent_HostAtOrAboveEightyGiB_DependsOnProcessorCount(int totalGiB, int thresholdPercent, int? processorCount, int? expected)
    {
        var totalBytes = totalGiB * 1024L * 1024 * 1024;
        var highMemoryLoadThresholdBytes = Encode(totalBytes, thresholdPercent);

        var result = GcMemoryLoadCalculator.ResolveHighMemoryLoadThresholdPercent(highMemoryLoadThresholdBytes, configuredHighPercent: null, () => processorCount);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 90)]
    [InlineData(null, 90)] // the flat-90 branch never needs the processor count, so an unknown count shouldn't stop it resolving
    public void ResolveHighMemoryLoadThresholdPercent_HostJustBelowEightyGiB_UsesFixedDefaultRegardlessOfProcessorCount(int? processorCount, int? expected)
    {
        var highMemoryLoadThresholdBytes = Encode(Total79GiB, 90);

        var result = GcMemoryLoadCalculator.ResolveHighMemoryLoadThresholdPercent(highMemoryLoadThresholdBytes, configuredHighPercent: null, () => processorCount);

        result.Should().Be(expected);
    }

    [Fact]
    public void ResolveHighMemoryLoadThresholdPercent_ConfiguredOverride_UnknownProcessorCountStillResolves()
    {
        // A configured override never needs the processor count either.
        var highMemoryLoadThresholdBytes = Encode(Total96GiB, 70);

        var result = GcMemoryLoadCalculator.ResolveHighMemoryLoadThresholdPercent(highMemoryLoadThresholdBytes, configuredHighPercent: 70, () => null);

        result.Should().Be(70);
    }

    [Fact]
    public void Calculate_HostAtOrAboveEightyGiB_UnknownProcessorCount_ReturnsNull()
    {
        var highMemoryLoadThresholdBytes = Encode(Total96GiB, 97);
        var totalAvailableMemoryBytes = Total96GiB;
        var memoryLoadBytes = Encode(Total96GiB, 42);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, configuredHighPercent: null, () => null);

        result.Should().BeNull();
    }

    [Fact]
    public void Calculate_HighMemoryLoadThresholdBytesIsZero_ReturnsNull()
    {
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 500, highMemoryLoadThresholdBytes: 0, totalAvailableMemoryBytes: Total8GiB, configuredHighPercent: null, GetTotalProcessorCount);

        result.Should().BeNull();
    }

    [Fact]
    public void Calculate_PathologicalHardLimitExceedsImpliedPhysicalMemory_ReturnsNull()
    {
        // A tiny highMemoryLoadThresholdBytes next to a huge totalAvailableMemoryBytes can never be consistent -
        // TotalAvailableMemoryBytes (heap_hard_limit) can never exceed total_physical_mem.
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 500, highMemoryLoadThresholdBytes: 1000, totalAvailableMemoryBytes: Total8GiB, configuredHighPercent: null, GetTotalProcessorCount);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(60)]
    [InlineData(99)]
    [InlineData(100)]
    public void Calculate_ExplicitHardLimitWhoseRatioLooksLikeACleanPercentage_RecoversTrueLoad(int loadPercent)
    {
        // A hard limit of 93.75% (15/16) of physical memory, over the default 90% threshold, inverts to a
        // byte-exact 96, so if we try to do anything funny by messing with the numbers, and looking for
        // rational values, it would fall through here
        var highMemoryLoadThresholdBytes = Encode(Total8GiB, 90);
        var totalAvailableMemoryBytes = Encode(Total8GiB, 93.75);
        var memoryLoadBytes = Encode(Total8GiB, loadPercent);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, configuredHighPercent: null, GetTotalProcessorCount);

        result.Should().Be(loadPercent);
    }

    [Fact]
    public void Calculate_ConfiguredOverrideWinsUnderAHardLimit()
    {
        // Configured override (70) under a 75% hard limit: neither the observed ratio (90) nor the runtime's
        // default formula (90) gives the right answer - only the configured override does.
        var highMemoryLoadThresholdBytes = Encode(Total8GiB, 70);
        var totalAvailableMemoryBytes = Encode(Total8GiB, 75);
        var memoryLoadBytes = Encode(Total8GiB, 42);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, configuredHighPercent: 70, GetTotalProcessorCount);

        result.Should().Be(42);
    }

    // Drift canary: exploits the fact that on a host with no GC hard limit, heap_hard_limit and total_physical_mem coincide, so
    // TryMeasureHighMemoryLoadThresholdPercent can *measure* high_memory_load_th directly from live GCMemoryInfo -
    // no assumptions involved. Comparing that measurement against production's ResolveHighMemoryLoadThresholdPercent
    // means that if a future runtime ever changes the default formula, the measurement follows it while
    // production's hardcoded copy does not, and the test fails. On a sub-80 GiB agent this only pins the flat 90 branch;
    // the processor-adjusted branch is covered only by the synthetic unit tests above, which can't drift-detect.
    [SkippableFact]
    public void DriftCanary_MeasuredDefaultHighMemoryLoadThresholdMatchesProductionPrediction()
    {
        var info = GC.GetGCMemoryInfo();
        Skip.If(info.HighMemoryLoadThresholdBytes <= 0 || info.TotalAvailableMemoryBytes <= 0, "No GC has run yet in this process");

        // Any GC hard-limit knob whose presence means heap_hard_limit no longer equals total_physical_mem, which is
        // what TryMeasureHighMemoryLoadThresholdPercent's inversion assumes. gc.cpp sets heap_hard_limit to the sum
        // of the per-generation limits, so the SOH/LOH/POH variants have to be covered too, not just the combined
        // knob. The public (AppContext/runtimeconfig) key for each of these is "System.GC." + the env suffix (see
        // gcconfig.h) - GCHighMemPercent doesn't create a hard limit at all (it only shifts the threshold), so it's
        // excluded here and instead fed through production's own parsers below.
        var gcHardLimitKnobsThatInvalidateTheMeasurement = new[]
        {
            ("GCHeapHardLimit", "System.GC.HeapHardLimit"),
            ("GCHeapHardLimitPercent", "System.GC.HeapHardLimitPercent"),
            ("GCHeapHardLimitSOH", "System.GC.HeapHardLimitSOH"),
            ("GCHeapHardLimitLOH", "System.GC.HeapHardLimitLOH"),
            ("GCHeapHardLimitPOH", "System.GC.HeapHardLimitPOH"),
            ("GCHeapHardLimitSOHPercent", "System.GC.HeapHardLimitSOHPercent"),
            ("GCHeapHardLimitLOHPercent", "System.GC.HeapHardLimitLOHPercent"),
            ("GCHeapHardLimitPOHPercent", "System.GC.HeapHardLimitPOHPercent"),
        };

        foreach (var (envSuffix, appContextKey) in gcHardLimitKnobsThatInvalidateTheMeasurement)
        {
            // Presence only, no parsing: a mis-parse here would silently re-enable the very ratio-inference
            // this canary exists to keep out of production.
            var envPresent = Environment.GetEnvironmentVariable("DOTNET_" + envSuffix) is not null
                           || Environment.GetEnvironmentVariable("COMPlus_" + envSuffix) is not null;
            envPresent.Should().BeFalse($"DOTNET_{envSuffix}/COMPlus_{envSuffix} is configured for this process; the canary only targets a host with no GC hard limit.");
            AppContext.GetData(appContextKey).Should().BeNull($"AppContext key {appContextKey} is configured for this process; the canary only targets a host with no GC hard limit.");
        }

        var measured = TryMeasureHighMemoryLoadThresholdPercent(info.HighMemoryLoadThresholdBytes, info.TotalAvailableMemoryBytes);
        Skip.If(measured is null, "Could not measure high_memory_load_th on this host (a hard limit may be in play despite no knob being detected, or the C#/C++ rounding didn't round-trip).");

        // Unlike the hard-limit knobs above, GCHighMemPercent doesn't invalidate the measurement - it only changes
        // the expected threshold - so instead of requiring its absence, resolve it through the same production
        // parsers used at runtime.
        var configuredHighPercent = GcMemoryLoadCalculator.ReadConfiguredHighMemoryLoadPercent();
        var predicted = GcMemoryLoadCalculator.ResolveHighMemoryLoadThresholdPercent(info.HighMemoryLoadThresholdBytes, configuredHighPercent, () => TotalProcessorCount.Value);

        measured.Should().Be(predicted);
        // The ratio-inference removed from production, kept only as a measurement tool for the drift canary above:
        // on a host with no GC hard limit, totalPhysicalMemoryBytes IS total_physical_mem, so inverting
        // thresholdBytes / totalPhysicalMemoryBytes and round-tripping it byte-exact really does recover
        // high_memory_load_th, with no assumptions about configuration or the default formula involved.
        static int? TryMeasureHighMemoryLoadThresholdPercent(long thresholdBytes, long totalPhysicalMemoryBytes)
        {
            var implied = (int)Math.Round((thresholdBytes / (double)totalPhysicalMemoryBytes) * 100);
            if (implied is < 1 or > 99)
            {
                return null;
            }

            var roundTrip = (long)((implied / 100.0) * totalPhysicalMemoryBytes);
            return roundTrip == thresholdBytes ? implied : null;
        }
    }

    // Mirrors how the GC encodes a percentage into bytes (compute_memory_settings, src/coreclr/gc/init.cpp as of
    // writing - see the pinned source reference on GcMemoryLoadCalculator.EightyGiBBytesAt90Percent):
    // `(uint64_t)(pct / 100 * total_physical_mem)`. Each test builds inputs the same way the runtime would, then
    // asserts we decode the original percentage back.
    private static long Encode(long total, double percent) => (long)((percent / 100.0) * total);
}
#endif
