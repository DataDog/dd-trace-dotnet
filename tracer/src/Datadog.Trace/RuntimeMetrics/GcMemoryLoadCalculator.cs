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
/// Tries to recover the true GC memory-load percentage (0-100) from <see cref="GCMemoryInfo"/>.
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

    private static readonly Lazy<bool> HasConfiguredHighMemoryLoadPercent = new(ReadHasConfiguredHighMemoryLoadPercent);

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
            HasConfiguredHighMemoryLoadPercent.Value);
    }

    [TestingAndPrivateOnly]
    internal static double? TryCalculate(long memoryLoadBytes, long highMemoryLoadThresholdBytes, long totalAvailableMemoryBytes, bool hasConfiguredHighMemoryLoadPercent)
    {
        if (highMemoryLoadThresholdBytes <= 0 || totalAvailableMemoryBytes <= 0)
        {
            // HighMemoryLoadThresholdBytes is 0 before the first GC has run, so we can't calculate anything
            return null;
        }

        // We need to recreate this flow from the GC: https://github.com/dotnet/runtime/blob/2cc068d0008c898c67578f2868bd5b17a64c6366/src/coreclr/gc/init.cpp#L1488C59-L1519
        if (hasConfiguredHighMemoryLoadPercent)
        {
            // If there's a high memory threshold, then bail out, as we can't reliably calculate without parsing all the values
            // identically to the GC, which is fragile
            return UseFallback(memoryLoadBytes, totalAvailableMemoryBytes);
        }

        // Check if the threshold is within the runtime's "easy" default formula".
        // Since the resolved percentage is always >= 90, the _implied_ total
        // here is always >= total_physical_mem, so "implied < 80GiB" implies "total_physical_mem < 80GiB".
        if (highMemoryLoadThresholdBytes > EightyGiBBytesAt90Percent)
        {
            // If we know we're > 80GB, then we can't accurately calculate the high memory load threshold percent without
            // getting the processors, which is a pain
            // If that processor count couldn't be reliably determined, we don't guess, we bail out.
            if (!Volatile.Read(ref _unableToResolveLogged))
            {
                Volatile.Write(ref _unableToResolveLogged, true);
                Log.Warning("Unable to resolve GC memory load percentage: total machine processor count is unknown (HighMemoryLoadThresholdBytes={HighMemoryLoadThresholdBytes})", highMemoryLoadThresholdBytes);
            }

            return UseFallback(memoryLoadBytes, totalAvailableMemoryBytes);
        }

        // heap_hard_limit (TotalAvailableMemoryBytes) can never exceed total_physical_mem. If the implied total
        // from our resolved threshold is smaller than TotalAvailableMemoryBytes, then we got something wrong in our
        // calculations, so bail out rather than publish a skewed value.
        // This should never be violated in practice, it's a safety check for our calculations
        const double highMemoryLoadThresholdPercentage = 90.0;
        const double highMemoryLoadThresholdRatio = highMemoryLoadThresholdPercentage / 100.0;

        var impliedTotalPhysicalMemoryBytes = highMemoryLoadThresholdBytes / highMemoryLoadThresholdRatio;

        // include some tolerance
        if (impliedTotalPhysicalMemoryBytes < totalAvailableMemoryBytes * 0.99)
        {
            if (!Volatile.Read(ref _unableToResolveLogged))
            {
                Volatile.Write(ref _unableToResolveLogged, true);
                Log.Error(
                    "Unable to resolve GC memory load percentage, calculated total available bytes {ImpliedTotalAvailableMemoryBytes} is less than provided GC value {TotalAvailableMemoryBytes} (MemoryLoadBytes={MemoryLoadBytes}, HighMemoryLoadThresholdBytes={HighMemoryLoadThresholdBytes})",
                    [
                        impliedTotalPhysicalMemoryBytes,
                        totalAvailableMemoryBytes,
                        memoryLoadBytes,
                        highMemoryLoadThresholdBytes
                    ]);
            }

            // Something is wrong with our calculation, shouldn't happen
            return UseFallback(memoryLoadBytes, totalAvailableMemoryBytes);
        }

        var memoryLoad = Math.Round(memoryLoadBytes * highMemoryLoadThresholdPercentage / highMemoryLoadThresholdBytes);
        return Math.Min(100d, Math.Max(0d, memoryLoad));

        // Fallback used when we can't reliably invert the threshold back to a total: fall back to the simple
        // pre-refactor calculation of load relative to TotalAvailableMemoryBytes. This is exactly correct unless a
        // GC hard limit is in play (TotalAvailableMemoryBytes then reflects heap_hard_limit rather than
        // total_physical_mem, which can inflate the result) - but that's still a better bet than the alternative.
        static double UseFallback(long memoryLoadBytes, long totalAvailableMemoryBytes)
            => Math.Min(100d, Math.Max(0d, memoryLoadBytes * 100.0 / totalAvailableMemoryBytes));
    }

    [TestingAndPrivateOnly]
    internal static bool ReadHasConfiguredHighMemoryLoadPercent()
    {
        // Check if either of the configs defined here are set, so we can bail out
        // https://github.com/dotnet/runtime/blob/2cc068d0008c898c67578f2868bd5b17a64c6366/src/coreclr/gc/gcconfig.h#L100
        try
        {
            var envValue = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHighMemPercent)
                        ?? EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHighMemPercent);

            // The runtime checks the environment variable first (gcenv.ee.cpp: GetGCHighMemPercent()). If it's
            // present at all - even "0", which means "unset" - it wins outright and the runtimeconfig knob below is
            // never consulted, so an explicit-but-unset env var can't fall back to a configured runtimeconfig value.
            if (envValue is not null)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading configured GC high memory load percent");
        }

        try
        {
            // The runtimeconfig knob (System.GC.HighMemoryPercent) is only consulted if the environment variable is unset.
            // runtimeconfig properties always reach AppContext as strings - anything else was set by user code
            // after startup, so the GC will never see it
            return AppContext.GetData("System.GC.HighMemoryPercent") is string;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error reading System.GC.HighMemoryPercent from AppContext");
        }

        return false;
    }
}
#endif
