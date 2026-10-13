// <copyright file="GcMemoryLoadCalculatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(
    PlatformKeys.DotNetGCHighMemPercent,
    PlatformKeys.ComPlusGCHighMemPercent,
    PlatformKeys.DotNetGCHeapHardLimit,
    PlatformKeys.ComPlusGCHeapHardLimit,
    PlatformKeys.DotNetGCHeapHardLimitPercent,
    PlatformKeys.ComPlusGCHeapHardLimitPercent,
    PlatformKeys.DotNetGCHeapHardLimitSOH,
    PlatformKeys.ComPlusGCHeapHardLimitSOH,
    PlatformKeys.DotNetGCHeapHardLimitLOH,
    PlatformKeys.ComPlusGCHeapHardLimitLOH,
    PlatformKeys.DotNetGCHeapHardLimitPOH,
    PlatformKeys.ComPlusGCHeapHardLimitPOH,
    PlatformKeys.DotNetGCHeapHardLimitSOHPercent,
    PlatformKeys.ComPlusGCHeapHardLimitSOHPercent,
    PlatformKeys.DotNetGCHeapHardLimitLOHPercent,
    PlatformKeys.ComPlusGCHeapHardLimitLOHPercent,
    PlatformKeys.DotNetGCHeapHardLimitPOHPercent,
    PlatformKeys.ComPlusGCHeapHardLimitPOHPercent)]
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

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, Config());

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
        // e.g. a memory-limited container with no explicit GC hard limit, where the runtime defaults to 75%.
        // Route A (the inferred-default-90 case) ignores TotalAvailableMemoryBytes / HasHeapHardLimit entirely,
        // so it recovers the true load exactly even though a hard limit is genuinely in play here.
        var highMemoryLoadThresholdBytes = AsBytes(Total8GiB, 90);
        var totalAvailableMemoryBytes = AsBytes(Total8GiB, 75);
        var memoryLoadBytes = AsBytes(Total8GiB, loadPercent);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, Config(hasHeapHardLimit: true));

        result.Should().Be(loadPercent);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(97)]
    [InlineData(99)]
    public void TryCalculate_KnownThresholdPercent_RecoversTrueLoad(int percent)
    {
        // Represents a resolved GCHighMemPercent from GC.GetConfigurationVariables() (.NET 8+, or .NET 10's
        // always-effective value even above 80GiB) - Route A is exact and completely ignores TotalAvailableMemoryBytes / HasHeapHardLimit.
        var highMemoryLoadThresholdBytes = AsBytes(Total96GiB, percent);
        var memoryLoadBytes = AsBytes(Total96GiB, 60);
        var configuration = Config(percent: percent, hasConfiguredPercent: true, hasHeapHardLimit: true);

        // TotalAvailableMemoryBytes is deliberately nonsensical (1 byte) to prove Route A never looks at it.
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes: 1, configuration);

        result.Should().Be(60);
    }

    [Fact]
    public void TryCalculate_ThresholdBelowEightyGiBAndUnconfigured_RecoversTrueLoad_RegressionTest()
    {
        // Regression test for the #9095 bug. TotalAvailableMemoryBytes (2000) is deliberately inconsistent with
        // a heap hard limit larger than physical memory (a decimal DOTNET_GCHeapHardLimit parsed as hexadecimal by the runtime).
        // Route A doesn't use TotalAvailableMemoryBytes at all, so it's unaffected: threshold=1000 implies total_physical_mem =
        // 1000/0.9 ~= 1111, so 500/1111 = 45%.
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 500, highMemoryLoadThresholdBytes: 1000, totalAvailableMemoryBytes: 2000, Config(hasHeapHardLimit: true));

        result.Should().Be(45);
    }

    [Theory]
    // An explicit threshold-percent override (DOTNET_GCHighMemPercent / System.GC.HighMemoryPercent) is
    // configured, but its resolved value is unknown (pre-.NET 8, or GetConfigurationVariables() unavailable) -
    // Route A is unavailable, so this falls back to Route B (load relative to TotalAvailableMemoryBytes).
    [InlineData(42, 70, 75, true, 56)]
    // >= 80GiB and unconfigured: the runtime's default formula could resolve anywhere in [90, 97], so Route A
    // is unavailable - falls back to Route B.
    [InlineData(42_000_000_000, 97_000_000_000, 100_000_000_000, false, 42)]
    // Route B clamps to [0, 100] - it must not publish a value above 100 just because the actual load already
    // exceeds TotalAvailableMemoryBytes...
    [InlineData(90, 70, 75, true, 100)]
    // ...nor below 0 for a negative load.
    [InlineData(-1000, 70, 75, true, 0)]
    public void TryCalculate_WhenThresholdPercentIsUnknown_FallsBackToRouteB(
        long memoryLoadBytes, long highMemoryLoadThresholdBytes, long totalAvailableMemoryBytes, bool hasConfiguredHighMemoryLoadPercent, double expected)
    {
        var configuration = Config(hasConfiguredPercent: hasConfiguredHighMemoryLoadPercent);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, configuration);

        result.Should().Be(expected);
    }

    [Fact]
    public void TryCalculate_HighMemoryLoadThresholdBytesIsZero_ReturnsNull()
    {
        // HighMemoryLoadThresholdBytes is 0 before the first GC has run
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 500, highMemoryLoadThresholdBytes: 0, totalAvailableMemoryBytes: Total8GiB, Config());

        result.Should().BeNull();
    }

    [Fact]
    public void TryCalculate_TotalAvailableMemoryBytesIsZero_RouteAStillResolves()
    {
        // Route A (the inferred-default-90 case) never reads TotalAvailableMemoryBytes, so a nonsensical 0
        // there doesn't stop it from resolving the true load.
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 500, highMemoryLoadThresholdBytes: 1000, totalAvailableMemoryBytes: 0, Config());

        result.Should().Be(45);
    }

    [Fact]
    public void TryCalculate_ThresholdPercentUnknownAndTotalAvailableMemoryBytesIsZero_ReturnsNull()
    {
        // Route A is unavailable (percent unknown) and Route B requires a positive TotalAvailableMemoryBytes.
        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 500, highMemoryLoadThresholdBytes: 1000, totalAvailableMemoryBytes: 0, Config(hasConfiguredPercent: true));

        result.Should().BeNull();
    }

    [Fact]
    public void TryCalculate_ThresholdPercentUnknownAndHardLimitInPlay_ReturnsNull()
    {
        // Neither route is sound: the percent is unknown (an override is configured but unresolved) and a GC
        // heap hard limit may be in play, so TotalAvailableMemoryBytes can't be trusted as a stand-in for
        // total_physical_mem either. We publish nothing rather than guess.
        var configuration = Config(hasConfiguredPercent: true, hasHeapHardLimit: true);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 500, highMemoryLoadThresholdBytes: 1000, totalAvailableMemoryBytes: 2000, configuration);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCalculate_AboveEightyGiBUnconfiguredWithHardLimit_ReturnsNull()
    {
        // >= 80GiB, unconfigured (percent unknown), and a hard limit is in play. Only reachable on .NET 8/9 -
        // .NET 10 always resolves a non-zero effective percent, so the percent is never unknown there once
        // GetConfigurationVariables() is authoritative.
        var configuration = Config(hasHeapHardLimit: true);

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: 42_000_000_000, highMemoryLoadThresholdBytes: 97_000_000_000, totalAvailableMemoryBytes: 100_000_000_000, configuration);

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

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, Config());

        result.Should().Be(100);
    }

    [Fact]
    public void TryCalculate_ClampsResultToLowerBound()
    {
        var highMemoryLoadThresholdBytes = AsBytes(Total8GiB, 90);
        var totalAvailableMemoryBytes = Total8GiB;

        var result = GcMemoryLoadCalculator.TryCalculate(memoryLoadBytes: -1000, highMemoryLoadThresholdBytes, totalAvailableMemoryBytes, Config());

        result.Should().Be(0);
    }

    [Fact]
    public void TryGetMemoryLoadPercentage_LiveGcMemoryInfo_ReturnsValueInRangeOrNull()
    {
        var info = GC.GetGCMemoryInfo();

        var result = GcMemoryLoadCalculator.TryGetMemoryLoadPercentage(info);

        // Legitimately null in various cases, but whenever it does resolve, it must be a valid percentage.
        if (result is { } value)
        {
            value.Should().BeInRange(0, 100);
        }
    }

    [Fact]
    public void Parse_BothKeysPresentNonZeroPercent_ResolvesThresholdFromConfig()
    {
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = 92L, ["GCHeapHardLimit"] = 0L };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: false, hasHeapHardLimitKnobFallback: false);

        result.HighMemoryLoadThresholdPercent.Should().Be(92);
        result.HasConfiguredHighMemoryLoadPercent.Should().BeTrue();
        result.HasHeapHardLimit.Should().BeFalse();
    }

    [Fact]
    public void Parse_RawPercentAbove99_ClampsTo99()
    {
        // The GC itself clamps GCHighMemPercent to 99 (gc.cpp: min(99u, ...)); in .NET 10+, we reproduce that clamp
        // caller-side since GetConfigurationVariables() returns the raw configured value, pre-clamp.
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = 150L, ["GCHeapHardLimit"] = 0L };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: false, hasHeapHardLimitKnobFallback: false);

        result.HighMemoryLoadThresholdPercent.Should().Be(99);
    }

    [Fact]
    public void Parse_RawPercentNegative_TreatsAsUnsignedOverflowAndClampsTo99()
    {
        // CoreCLR parses DOTNET_GCHighMemPercent as an unsigned 64-bit hex value (gcenv.ee.cpp: u16_strtoui64(..., 16))
        // then stores it as static_cast<int64_t>, so an oversized hex value (e.g. 0xFFFFFFFFFFFFFFFF) surfaces here
        // as negative - GC then caps the effective threshold at 99 (rather than clamping to 0)
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = -1L, ["GCHeapHardLimit"] = 0L };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: false, hasHeapHardLimitKnobFallback: false);

        result.HighMemoryLoadThresholdPercent.Should().Be(99);
        result.HasConfiguredHighMemoryLoadPercent.Should().BeTrue();
    }

    [Fact]
    public void Parse_PercentZeroButHardLimitSet_ResolvesHardLimitOnly()
    {
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = 0L, ["GCHeapHardLimit"] = 1_073_741_824L };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: false, hasHeapHardLimitKnobFallback: false);

        result.HighMemoryLoadThresholdPercent.Should().BeNull();
        result.HasConfiguredHighMemoryLoadPercent.Should().BeFalse();
        result.HasHeapHardLimit.Should().BeTrue();
    }

    [Fact]
    public void Parse_BothZero_ResolvesUnconfiguredIgnoringFallbacks()
    {
        // When GetConfigurationVariables() is available, it's authoritative - the fallback flags (which only
        // exist for pre-.NET 8) must be ignored entirely, not merged in.
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = 0L, ["GCHeapHardLimit"] = 0L };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: true, hasHeapHardLimitKnobFallback: true);

        result.HighMemoryLoadThresholdPercent.Should().BeNull();
        result.HasConfiguredHighMemoryLoadPercent.Should().BeFalse();
        result.HasHeapHardLimit.Should().BeFalse();
    }

    [Theory]
    [CombinatorialData]
    public void Parse_NullDictionary_FallsBackToProvidedFlags(bool hasConfiguredPercent, bool hasHardLimitKnob)
    {
        var result = GcMemoryLoadCalculator.Parse(null, hasConfiguredPercent, hasHardLimitKnob);

        result.HighMemoryLoadThresholdPercent.Should().BeNull();
        result.HasConfiguredHighMemoryLoadPercent.Should().Be(hasConfiguredPercent);
        result.HasHeapHardLimit.Should().Be(hasHardLimitKnob);
    }

    [Fact]
    public void Parse_EmptyDictionary_FallsBackToProvidedFlags()
    {
        var result = GcMemoryLoadCalculator.Parse(new Dictionary<string, object>(), hasConfiguredHighMemoryLoadPercentFallback: true, hasHeapHardLimitKnobFallback: false);

        result.HighMemoryLoadThresholdPercent.Should().BeNull();
        result.HasConfiguredHighMemoryLoadPercent.Should().BeTrue();
        result.HasHeapHardLimit.Should().BeFalse();
    }

    [Fact]
    public void Parse_MissingHeapHardLimitKey_FallsBackToProvidedFlags()
    {
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = 90L };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: false, hasHeapHardLimitKnobFallback: true);

        result.HighMemoryLoadThresholdPercent.Should().BeNull();
        result.HasConfiguredHighMemoryLoadPercent.Should().BeFalse();
        result.HasHeapHardLimit.Should().BeTrue();
    }

    [Fact]
    public void Parse_WrongBoxedType_FallsBackToProvidedFlags()
    {
        // Defensive: integer GC config knobs are always boxed as long. Seeing an int here would mean an
        // unexpected runtime change, not a value we should trust.
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = 90, ["GCHeapHardLimit"] = 0 };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: true, hasHeapHardLimitKnobFallback: true);

        result.HighMemoryLoadThresholdPercent.Should().BeNull();
        result.HasConfiguredHighMemoryLoadPercent.Should().BeTrue();
        result.HasHeapHardLimit.Should().BeTrue();
    }

    [Fact]
    public void Parse_WhenPercentUntrusted_UsesAuthoritativeHardLimitOverKnobFallback()
    {
        // For .NET 7 GCHighMemPercent buggily reports 0 (dotnet/runtime#84198) so the
        // presence-only fallback would normally win, but the resolved container-derived GCHeapHardLimit is
        // reliable and overrides it - the knob-presence check can't see an implicit limit at all.
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = 0L, ["GCHeapHardLimit"] = 1_610_612_736L };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: true, hasHeapHardLimitKnobFallback: false, canTrustHighMemoryLoadPercent: false);

        result.HighMemoryLoadThresholdPercent.Should().BeNull();
        result.HasConfiguredHighMemoryLoadPercent.Should().BeTrue();
        result.HasHeapHardLimit.Should().BeTrue();
    }

    [Theory]
    [CombinatorialData]
    public void Parse_WhenPercentUntrusted_NeverResolvesPercent(bool hasConfiguredPercentFallback)
    {
        // Even a plausible non-zero value must be discarded when it can't be trusted, and the resolved 0
        // limit beats a "true" knob fallback.
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = 92L, ["GCHeapHardLimit"] = 0L };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredPercentFallback, hasHeapHardLimitKnobFallback: true, canTrustHighMemoryLoadPercent: false);

        result.HighMemoryLoadThresholdPercent.Should().BeNull();
        result.HasConfiguredHighMemoryLoadPercent.Should().Be(hasConfiguredPercentFallback);
        result.HasHeapHardLimit.Should().BeFalse();
    }

    [Theory]
    [CombinatorialData]
    public void Parse_WhenPercentUntrustedAndLimitKeyUnusable_FallsBackEntirely(bool hasHardLimitKnob)
    {
        // Defensive: integer GC config knobs are always boxed as long. Seeing an int here would mean an
        // unexpected runtime change, not a value we should trust - fall back to the knob presence check.
        var vars = new Dictionary<string, object> { ["GCHighMemPercent"] = 0L, ["GCHeapHardLimit"] = 0 };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: false, hasHeapHardLimitKnobFallback: hasHardLimitKnob, canTrustHighMemoryLoadPercent: false);

        result.HasHeapHardLimit.Should().Be(hasHardLimitKnob);
    }

    [Fact]
    public void Parse_WhenPercentTrustedAndPercentKeyMissing_IgnoresAuthoritativeHardLimit()
    {
        // Regression lock for the strict .NET 8+ behaviour: we never trust half a dictionary there, even
        // though the limit alone would be usable - .NET 8+ must remain bit-identical to before this change.
        var vars = new Dictionary<string, object> { ["GCHeapHardLimit"] = 1_073_741_824L };

        var result = GcMemoryLoadCalculator.Parse(vars, hasConfiguredHighMemoryLoadPercentFallback: false, hasHeapHardLimitKnobFallback: false);

        result.HasHeapHardLimit.Should().BeFalse();
    }

#if NET7_0_OR_GREATER
    [Fact]
    public void TryReadConfigurationVariables_Net7OrGreater_MatchesDirectCall()
    {
        var expected = GC.GetConfigurationVariables();

        // Today, GC always boxes values as longs, if that changes, our Parse code would fail, so make it explicit
        expected.Should().NotBeNull();
        expected["GCHighMemPercent"].Should().BeOfType<long>();
        expected["GCHeapHardLimit"].Should().BeOfType<long>();

        // Make sure we match
        var result = GcMemoryLoadCalculator.TryReadConfigurationVariables();
        result.Should().NotBeNull();
        result!["GCHighMemPercent"].Should().Be(expected["GCHighMemPercent"]);
        result!["GCHeapHardLimit"].Should().Be(expected["GCHeapHardLimit"]);
    }
#else
    [SkippableFact]
    public void TryReadConfigurationVariables_Net6_ReturnsNull()
    {
        // The net6.0 leg can roll forward onto a newer runtime, where the API exists and the gate now lets
        // it through - only assert the .NET 6 behaviour when we're genuinely on .NET 6.
        Skip.If(FrameworkDescription.Instance.RuntimeVersion.Major >= 7);

        GcMemoryLoadCalculator.TryReadConfigurationVariables().Should().BeNull();
    }
#endif

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

    [Fact]
    public void ReadHasHeapHardLimitKnob_NoConfig_ReturnsFalse()
    {
        ClearHeapHardLimitAppContextData();

        var result = GcMemoryLoadCalculator.ReadHasConfiguredHeapHardLimit();

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(PlatformKeys.DotNetGCHeapHardLimit)]
    [InlineData(PlatformKeys.ComPlusGCHeapHardLimit)]
    [InlineData(PlatformKeys.DotNetGCHeapHardLimitPercent)]
    [InlineData(PlatformKeys.ComPlusGCHeapHardLimitPercent)]
    [InlineData(PlatformKeys.DotNetGCHeapHardLimitSOH)]
    [InlineData(PlatformKeys.ComPlusGCHeapHardLimitSOH)]
    [InlineData(PlatformKeys.DotNetGCHeapHardLimitLOH)]
    [InlineData(PlatformKeys.ComPlusGCHeapHardLimitLOH)]
    [InlineData(PlatformKeys.DotNetGCHeapHardLimitPOH)]
    [InlineData(PlatformKeys.ComPlusGCHeapHardLimitPOH)]
    [InlineData(PlatformKeys.DotNetGCHeapHardLimitSOHPercent)]
    [InlineData(PlatformKeys.ComPlusGCHeapHardLimitSOHPercent)]
    [InlineData(PlatformKeys.DotNetGCHeapHardLimitLOHPercent)]
    [InlineData(PlatformKeys.ComPlusGCHeapHardLimitLOHPercent)]
    [InlineData(PlatformKeys.DotNetGCHeapHardLimitPOHPercent)]
    [InlineData(PlatformKeys.ComPlusGCHeapHardLimitPOHPercent)]
    public void ReadHasHeapHardLimitKnob_EnvVarSet_ReturnsTrue(string envVar)
    {
        ClearHeapHardLimitAppContextData();
        Environment.SetEnvironmentVariable(envVar, "1073741824");

        var result = GcMemoryLoadCalculator.ReadHasConfiguredHeapHardLimit();

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("System.GC.HeapHardLimit")]
    [InlineData("System.GC.HeapHardLimitPercent")]
    [InlineData("System.GC.HeapHardLimitSOH")]
    [InlineData("System.GC.HeapHardLimitLOH")]
    [InlineData("System.GC.HeapHardLimitPOH")]
    [InlineData("System.GC.HeapHardLimitSOHPercent")]
    [InlineData("System.GC.HeapHardLimitLOHPercent")]
    [InlineData("System.GC.HeapHardLimitPOHPercent")]
    public void ReadHasHeapHardLimitKnob_RuntimeConfigKnobSetAsString_ReturnsTrue(string appContextKey)
    {
        ClearHeapHardLimitAppContextData();

        try
        {
            AppContext.SetData(appContextKey, "1073741824");

            var result = GcMemoryLoadCalculator.ReadHasConfiguredHeapHardLimit();

            result.Should().BeTrue();
        }
        finally
        {
            ClearHeapHardLimitAppContextData();
        }
    }
#endif

    // Mirrors how the GC encodes a percentage into bytes (compute_memory_settings, src/coreclr/gc/init.cpp as of
    // writing - see the pinned source reference on GcMemoryLoadCalculator.EightyGiBBytesAt90Percent):
    // `(uint64_t)(pct / 100 * total_physical_mem)`. Each test builds inputs the same way the runtime would, then
    // asserts we decode the original percentage back.
    private static long AsBytes(long total, double percent) => (long)((percent / 100.0) * total);

    private static GcMemoryLoadCalculator.GcMemoryConfiguration Config(int? percent = null, bool hasConfiguredPercent = false, bool hasHeapHardLimit = false)
        => new(percent, hasConfiguredPercent, hasHeapHardLimit);

    // AppContext.SetData is public at runtime on every supported TFM, but the net6.0 (and earlier) reference
    // assemblies used to compile this project don't declare it. Below net7.0, nothing in this test binary can
    // set the value in the first place, so clearing it is a no-op.
#if NET7_0_OR_GREATER
    private static void ClearAppContextData() => AppContext.SetData("System.GC.HighMemoryPercent", null);

    private static void ClearHeapHardLimitAppContextData()
    {
        AppContext.SetData("System.GC.HeapHardLimit", null);
        AppContext.SetData("System.GC.HeapHardLimitPercent", null);
        AppContext.SetData("System.GC.HeapHardLimitSOH", null);
        AppContext.SetData("System.GC.HeapHardLimitLOH", null);
        AppContext.SetData("System.GC.HeapHardLimitPOH", null);
        AppContext.SetData("System.GC.HeapHardLimitSOHPercent", null);
        AppContext.SetData("System.GC.HeapHardLimitLOHPercent", null);
        AppContext.SetData("System.GC.HeapHardLimitPOHPercent", null);
    }
#endif
}
#endif
