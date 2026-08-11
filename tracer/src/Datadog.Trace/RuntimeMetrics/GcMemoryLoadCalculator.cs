// <copyright file="GcMemoryLoadCalculator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.RuntimeMetrics;

/// <summary>
/// Recovers the true GC memory-load percentage (0-100) from <see cref="GCMemoryInfo"/>.
/// <see cref="GCMemoryInfo.MemoryLoadBytes"/> and <see cref="GCMemoryInfo.HighMemoryLoadThresholdBytes"/> are both
/// scaled by the GC's <c>total_physical_mem</c>, but <see cref="GCMemoryInfo.TotalAvailableMemoryBytes"/> switches
/// to <c>heap_hard_limit</c> whenever a GC hard limit is in play (e.g. a memory-limited container without explicit
/// GC configuration, where the runtime defaults the limit to 75% of physical memory).
/// See <c>GCHeap::GetMemoryInfo</c> in src/coreclr/gc/gc.cpp.
/// </summary>
internal static class GcMemoryLoadCalculator
{
    // gc_heap::compute_memory_settings() only applies its ">= 80GB" branch above this threshold.
    // The value here is pre-scaled by the default high-memory-load percentage (90%) so the comparison
    // below is a plain integer comparison, not a division.
    private const long EightyGiBBytesAt90Percent = 80L * 1024 * 1024 * 1024 * 9 / 10;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GcMemoryLoadCalculator));

    // high_memory_load_th is fixed for the lifetime of the GC configuration it was resolved from, so the
    // configured override (if any) only needs to be read once.
    private static readonly Lazy<int?> ConfiguredHighMemoryLoadPercent = new(ReadConfiguredHighMemoryLoadPercent);
    private static readonly Func<int?> GetTotalProcessorCount = () => TotalProcessorCount.Value;

    private static bool _unableToResolveLogged;

    /// <summary>
    /// Gets the GC memory load as a 0-100 percentage, or <c>null</c> if it cannot be reliably determined.
    /// </summary>
    public static double? TryGetMemoryLoadPercentage(in GCMemoryInfo info)
    {
        return TryCalculate(
            info.MemoryLoadBytes,
            info.HighMemoryLoadThresholdBytes,
            info.TotalAvailableMemoryBytes,
            ConfiguredHighMemoryLoadPercent.Value,
            GetTotalProcessorCount);
    }

    [TestingAndPrivateOnly]
    internal static double? TryCalculate(long memoryLoadBytes, long highMemoryLoadThresholdBytes, long totalAvailableMemoryBytes, int? configuredHighPercent, Func<int?> getTotalProcessorCount)
    {
        if (highMemoryLoadThresholdBytes <= 0 || totalAvailableMemoryBytes <= 0)
        {
            // HighMemoryLoadThresholdBytes is 0 before the first GC has run, so we can't calculate anything
            return null;
        }

        var highPercent = ResolveHighMemoryLoadThresholdPercent(highMemoryLoadThresholdBytes, configuredHighPercent, getTotalProcessorCount);
        if (highPercent is null)
        {
            return null;
        }

        // heap_hard_limit (TotalAvailableMemoryBytes) can never exceed total_physical_mem. If the implied total
        // from our resolved threshold is smaller than TotalAvailableMemoryBytes, then we got something wrong in our
        // calculations, so bail out rather than publish a skewed value.
        // This should never be violated, it's just a safety check
        var impliedTotalPhysicalMem = highMemoryLoadThresholdBytes * 100.0 / highPercent.Value;
        if (impliedTotalPhysicalMem < totalAvailableMemoryBytes * 0.99)
        {
            if (!Volatile.Read(ref _unableToResolveLogged))
            {
                Volatile.Write(ref _unableToResolveLogged, true);
                Log.Warning(
                    "Unable to resolve GC memory load percentage, implied total {ImpliedTotal} is less than total available bytes {TotalAvailableMemoryBytes} (MemoryLoadBytes={MemoryLoadBytes}, HighMemoryLoadThresholdBytes={HighMemoryLoadThresholdBytes}, ConfiguredHighPercent={ConfiguredHighPercent})",
                    [
                        impliedTotalPhysicalMem,
                        totalAvailableMemoryBytes,
                        memoryLoadBytes,
                        highMemoryLoadThresholdBytes,
                        configuredHighPercent
                    ]);
            }

            return null;
        }

        var memoryLoad = Math.Round(memoryLoadBytes * (double)highPercent.Value / highMemoryLoadThresholdBytes);
        return Math.Min(100d, Math.Max(0d, memoryLoad));
    }

    [TestingAndPrivateOnly]
    internal static int? ResolveHighMemoryLoadThresholdPercent(long highMemoryLoadThresholdBytes, int? configuredHighPercent, Func<int?> getTotalProcessorCount)
    {
        // We need to recreate this flow from the GC: https://github.com/dotnet/runtime/blob/2cc068d0008c898c67578f2868bd5b17a64c6366/src/coreclr/gc/init.cpp#L1488C59-L1519

        // An explicit override (from env/AppContext) always wins
        if (configuredHighPercent is { } configured)
        {
            return configured;
        }

        // Otherwise, the threshold should be the runtime's default formula.
        // Since the resolved percentage is always >= 90, the _implied_ total
        // here is always >= total_physical_mem, so "implied < 80GiB" implies "total_physical_mem < 80GiB".
        if (highMemoryLoadThresholdBytes < EightyGiBBytesAt90Percent)
        {
            return 90;
        }

        // If we know we're > 80GB, but we can't get the processor count, then we can't accurately
        // calculate the high memory load threshold percent
        if (getTotalProcessorCount() is not { } processorCount)
        {
            // If that processor count couldn't be reliably determined, we don't guess, we bail out.
            if (!Volatile.Read(ref _unableToResolveLogged))
            {
                Volatile.Write(ref _unableToResolveLogged, true);
                Log.Warning(
                    "Unable to resolve GC memory load percentage: total machine processor count is unknown (HighMemoryLoadThresholdBytes={HighMemoryLoadThresholdBytes}, ConfiguredHighPercent={ConfiguredHighPercent})",
                    highMemoryLoadThresholdBytes,
                    configuredHighPercent);
            }

            return null;
        }

        // Calculating from https://github.com/dotnet/runtime/blob/2cc068d0008c898c67578f2868bd5b17a64c6366/src/coreclr/gc/init.cpp#L1508
        var availableMemThreshold = Math.Min(10, 3 + (int)(47f / Math.Max(1, processorCount)));
        return 100 - availableMemThreshold;
    }

    [TestingAndPrivateOnly]
    internal static int? ParseEnvHighMemPercent(ReadOnlySpan<char> envValue)
    {
        // GCToEEInterface::GetIntConfigValue reads the env knob via u16_strtoui64(value, &end, 16) - a 64-bit
        // parse on every platform (Windows: _wcstoui64, Unix: PAL__wcstoui64 -> strtoull) - and treats ERANGE
        // (overflow past ulong.MaxValue) as "not specified at all" rather than clamping
        if (!TryParseCStyleUnsignedInteger(envValue, numberBase: 16, out var parsed, out var overflowed) || overflowed)
        {
            return null;
        }

        return ToGcHighMemPercent(parsed);
    }

    [TestingAndPrivateOnly]
    internal static int? ParseAppContextHighMemPercent(object? appContextValue)
    {
        // runtimeconfig properties always reach AppContext as strings - anything else was set by user code
        // after startup, so the GC never saw it
        if (appContextValue is not string stringValue)
        {
            return null;
        }

        // Configuration::GetKnobULONGLONGValue reads the runtimeconfig knob via u16_strtoui64(value, nullptr, 0)
        // (base 0 - decimal, or hex/octal by prefix) and ignores the return value's ERANGE (errno) entirely, so
        // an out-of-range value saturates to UINT64_MAX and gets clamped to 99 below
        TryParseCStyleUnsignedInteger(stringValue.AsSpan(), numberBase: 0, out var parsed, out _);
        return ToGcHighMemPercent(parsed);
    }

    // compute_memory_settings() (see the pinned source reference on EightyGiBBytesAt90Percent above) reads the
    // config value into a 32-bit integer, so the high bits are silently dropped, then treats 0 as "not
    // configured" and clamps the result to 99.
    private static int? ToGcHighMemPercent(ulong configValue)
    {
        var truncated = (uint)configValue;
        return truncated == 0 ? null : (int)Math.Min(99u, truncated);
    }

    // Emulates the C runtime's strtoull(value, &end, numberBase), which both u16_strtoui64(..., 16) (env,
    // via GetIntConfigValue) and u16_strtoui64(..., 0) (runtimeconfig, via GetKnobULONGLONGValue) build on.
    // Parsing stops at the first invalid character rather than requiring the whole span to match, ERANGE
    // (overflow past ulong.MaxValue) is reported separately rather than failing the parse, and a leading '-'
    // negates the result within the unsigned range rather than being rejected - but only when the magnitude
    // didn't itself overflow: strtoull() saturates to ULLONG_MAX on ERANGE *before* the sign would be applied,
    // so an overflowing negative value must stay saturated, not get negated down to 1.
    private static bool TryParseCStyleUnsignedInteger(ReadOnlySpan<char> value, int numberBase, out ulong result, out bool overflowed)
    {
        result = 0;
        overflowed = false;

        var i = 0;
        while (i < value.Length && char.IsWhiteSpace(value[i]))
        {
            i++;
        }

        var negative = false;
        if (i < value.Length && (value[i] == '+' || value[i] == '-'))
        {
            negative = value[i] == '-';
            i++;
        }

        if (numberBase is 16 or 0 &&
            i + 1 < value.Length && value[i] == '0' && (value[i + 1] is 'x' or 'X') &&
            i + 2 < value.Length && HexDigitValue(value[i + 2]) >= 0)
        {
            numberBase = 16;
            i += 2;
        }
        else if (numberBase == 0)
        {
            numberBase = i < value.Length && value[i] == '0' ? 8 : 10;
        }

        var digitsConsumed = 0;
        for (; i < value.Length; i++)
        {
            var digit = HexDigitValue(value[i]);
            if (digit < 0 || digit >= numberBase)
            {
                break;
            }

            digitsConsumed++;

            if (overflowed)
            {
                continue;
            }

            if (result > (ulong.MaxValue - (ulong)digit) / (ulong)numberBase)
            {
                overflowed = true;
                result = ulong.MaxValue;
                continue;
            }

            result = (result * (ulong)numberBase) + (ulong)digit;
        }

        if (digitsConsumed == 0)
        {
            result = 0;
            return false;
        }

        if (negative && !overflowed)
        {
            result = unchecked(0UL - result);
        }

        return true;

        // Returns the digit's value for 0-9/a-f/A-F, or -1 if the char isn't a hex digit.
        static int HexDigitValue(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
    }

    [TestingAndPrivateOnly]
    internal static int? ReadConfiguredHighMemoryLoadPercent()
    {
        // Read the configs defined here: https://github.com/dotnet/runtime/blob/2cc068d0008c898c67578f2868bd5b17a64c6366/src/coreclr/gc/gcconfig.h#L100
        try
        {
            var envValue = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHighMemPercent)
                        ?? EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHighMemPercent);

            // The runtime checks the environment variable first (gcenv.ee.cpp: GetGCHighMemPercent()). If it's
            // present at all - even "0", which means "unset" - it wins outright and the runtimeconfig knob below is
            // never consulted, so an explicit-but-unset env var can't fall back to a configured runtimeconfig value.
            if (envValue is not null)
            {
                return ParseEnvHighMemPercent(envValue.AsSpan());
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading configured GC high memory load percent");
        }

        try
        {
            // The runtimeconfig knob (System.GC.HighMemoryPercent) is only consulted if the environment variable is unset.
            return ParseAppContextHighMemPercent(AppContext.GetData("System.GC.HighMemoryPercent"));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error reading System.GC.HighMemoryPercent from AppContext");
        }

        return null;
    }
}
#endif
