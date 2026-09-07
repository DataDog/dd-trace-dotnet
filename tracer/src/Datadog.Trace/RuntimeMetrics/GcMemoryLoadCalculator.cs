// <copyright file="GcMemoryLoadCalculator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
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

    private static readonly Lazy<GcMemoryConfiguration> Configuration = new(ReadConfiguration);

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
            Configuration.Value);
    }

    [TestingAndPrivateOnly]
    internal static double? TryCalculate(long memoryLoadBytes, long highMemoryLoadThresholdBytes, long totalAvailableMemoryBytes, GcMemoryConfiguration configuration)
    {
        if (highMemoryLoadThresholdBytes <= 0)
        {
            // HighMemoryLoadThresholdBytes is 0 before the first GC has run, so we can't calculate anything
            return null;
        }

        var highMemoryLoadThresholdPercent = configuration.HighMemoryLoadThresholdPercent;

        if (highMemoryLoadThresholdPercent is null
            && !configuration.HasConfiguredHighMemoryLoadPercent
            && highMemoryLoadThresholdBytes <= EightyGiBBytesAt90Percent)
        {
            // Unconfigured, and the runtime's default formula (init.cpp: compute_memory_settings()) only resolves
            // above 90 once physical memory reaches 80GiB, so below that threshold it must be exactly 90.
            highMemoryLoadThresholdPercent = 90;
        }

        if (highMemoryLoadThresholdPercent is { } percent)
        {
            // Route A: MemoryLoadBytes and HighMemoryLoadThresholdBytes are both scaled by total_physical_mem, so
            // it cancels out of this division, making the result exact regardless of whether a GC hard limit is in play.
            var memoryLoad = Math.Round(memoryLoadBytes * (double)percent / highMemoryLoadThresholdBytes);
            return Clamp(memoryLoad);
        }

        if (!configuration.HasHeapHardLimit && totalAvailableMemoryBytes > 0)
        {
            // Route B: no GC hard limit is explicitly configured, so TotalAvailableMemoryBytes ==
            // total_physical_mem (unless we're running in containers - that triggers a 75% scaling issue)
            // but as that's pre-existing, we're glossing over it...
            return Clamp(memoryLoadBytes * 100.0 / totalAvailableMemoryBytes);
        }

        // Neither route is sound: the threshold percentage is unknown (unconfigured and >= 80GiB physical memory)
        // or a GC hard limit might be set, so TotalAvailableMemoryBytes can't be trusted either. Publishing a
        // value here would mean guessing, so we don't - we bail out instead.
        if (totalAvailableMemoryBytes > 0 && !Volatile.Read(ref _unableToResolveLogged))
        {
            Volatile.Write(ref _unableToResolveLogged, true);
            if (configuration.HasHeapHardLimit)
            {
                Log.Warning(
                    "Unable to resolve GC memory load percentage: the high memory load threshold percentage is unknown and a GC heap hard limit may be set (HighMemoryLoadThresholdBytes={HighMemoryLoadThresholdBytes}, TotalAvailableMemoryBytes={TotalAvailableMemoryBytes})",
                    highMemoryLoadThresholdBytes,
                    totalAvailableMemoryBytes);
            }
            else
            {
                Log.Warning(
                    "Unable to resolve GC memory load percentage: the high memory load threshold percentage is unknown (HighMemoryLoadThresholdBytes={HighMemoryLoadThresholdBytes}, TotalAvailableMemoryBytes={TotalAvailableMemoryBytes})",
                    highMemoryLoadThresholdBytes,
                    totalAvailableMemoryBytes);
            }
        }

        return null;

        static double Clamp(double memoryLoad) => Math.Min(100d, Math.Max(0d, memoryLoad));
    }

    [TestingAndPrivateOnly]
    internal static GcMemoryConfiguration ReadConfiguration()
    {
        var configurationVariables = TryReadConfigurationVariables();

        // On .NET 7, GCHighMemPercent in the dictionary is broken - it reports 0 even when explicitly
        // configured, so we can only trust it on .NET 8+. GCHeapHardLimit _is_ correct on .NET 7.
        return Parse(
            configurationVariables,
            canTrustHighMemoryLoadPercent: FrameworkDescription.Instance.RuntimeVersion.Major >= 8);
    }

    /// <summary>
    /// Parse the values returned by <see cref="TryReadConfigurationVariables"/>
    /// </summary>
    /// <param name="configurationVariables">The variables to parse (or null if they couldn't be found)</param>
    /// <param name="hasConfiguredHighMemoryLoadPercentFallback">The value to use as the fallback for hasConfiguredHighMemoryLoadPercent (testing only)</param>
    /// <param name="hasHeapHardLimitKnobFallback">The value to use as the fallback for hasHeapHardLimit (testing only)</param>
    /// <param name="canTrustHighMemoryLoadPercent">Whether the <c>GCHighMemPercent</c> entry can be trusted (<c>false</c> on .NET 7, where it always reports 0)</param>
    [TestingAndPrivateOnly]
    internal static GcMemoryConfiguration Parse(
        IReadOnlyDictionary<string, object>? configurationVariables,
        bool? hasConfiguredHighMemoryLoadPercentFallback = null,
        bool? hasHeapHardLimitKnobFallback = null,
        bool canTrustHighMemoryLoadPercent = true)
    {
        // Don't use GCHighMemPercent unless it can be trusted.
        long rawPercent = 0;
        var hasPercent = canTrustHighMemoryLoadPercent
                       && TryGetInt64(configurationVariables, "GCHighMemPercent", out rawPercent);
        var hasLimit = TryGetInt64(configurationVariables, "GCHeapHardLimit", out var rawLimit);

        if (hasPercent && hasLimit)
        {
            // In .NET 10, rawPercent contains the "real" value. Never 0.
            // In .NET 8/9 it has a value _only_ if specifically configured, otherwise it's 0 (so 0 == not configured).
            // We need to clamp it though, as values could exceed 99, and can overflow - we handle this the same way the GC does in gc.cpp
            //
            // In all cases, rawLimit != 0 if there is a heap hard limit at all (explicit configured or a container-derived default)
            var unsignedPercent = unchecked((ulong)rawPercent);
            int? percent = rawPercent != 0
                               ? unsignedPercent > 99 ? 99 : (int)unsignedPercent
                               : null;
            return new GcMemoryConfiguration(
                highMemoryLoadThresholdPercent: percent,
                hasConfiguredHighMemoryLoadPercent: rawPercent != 0,
                hasHeapHardLimit: rawLimit != 0);
        }

        // Only .NET 7 trusts half a dictionary: there GCHeapHardLimit is reliable while GCHighMemPercent isn't,
        // and the resolved limit beats a presence check that can't see an implicit container limit. Note that an
        // explicit limit set to exactly total physical memory now bails rather than answering - the safe direction.
        // On .NET 8+ we only reach here if the dictionary itself was unusable, so keep the historical behaviour.
        var hasHeapHardLimit = !canTrustHighMemoryLoadPercent && hasLimit
                                    ? rawLimit != 0 // .NET 7 path
                                    : hasHeapHardLimitKnobFallback ?? ReadHasConfiguredHeapHardLimit(); // Everything else

        return new GcMemoryConfiguration(
            highMemoryLoadThresholdPercent: null,
            hasConfiguredHighMemoryLoadPercent: hasConfiguredHighMemoryLoadPercentFallback ?? ReadHasConfiguredHighMemoryLoadPercent(),
            hasHeapHardLimit: hasHeapHardLimit);

        static bool TryGetInt64(IReadOnlyDictionary<string, object>? variables, string name, out long value)
        {
            // Integer GC config knobs are always boxed as System.Int64:
            // https://github.com/dotnet/runtime/blob/main/src/coreclr/System.Private.CoreLib/src/System/GC.CoreCLR.cs
            // Anything else means an unexpected runtime change, not a value we should trust.
            if (variables is not null && variables.TryGetValue(name, out var raw) && raw is long knob)
            {
                value = knob;
                return true;
            }

            value = 0;
            return false;
        }
    }

    [TestingAndPrivateOnly]
    internal static IReadOnlyDictionary<string, object>? TryReadConfigurationVariables()
    {
        // GC.GetConfigurationVariables() was added in .NET 7 (dotnet/runtime#70514) and genuinely doesn't
        // exist on .NET 6, so short-circuit rather than paying for a reflection lookup that can only fail.
        // Note that the values we read are buggy in various different ways on < .NET 10, which is handled in Parse().
        if (FrameworkDescription.Instance.RuntimeVersion.Major < 7)
        {
            return null;
        }

        try
        {
            // We use reflection instead of ducktyping as doesn't make sense to pay the one-off cost of Reflection.Emit
            // when we are only going to invoke it once
            var methodInfo = typeof(GC).GetMethod("GetConfigurationVariables", BindingFlags.Public | BindingFlags.Static);
            if (methodInfo is null)
            {
                Log.Debug("GC.GetConfigurationVariables() is not available on this runtime; GC hard-limit and high-memory-load-percent detection will fall back to presence-only checks.");
                return null;
            }

            var getConfigurationVariables = methodInfo.CreateDelegate<Func<IReadOnlyDictionary<string, object>>>();
            return getConfigurationVariables();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calling GC.GetConfigurationVariables()");
            return null;
        }
    }

    [TestingAndPrivateOnly]
    internal static bool ReadHasConfiguredHighMemoryLoadPercent()
    {
        // Check if either of the configs defined here are set, so we can bail out
        // https://github.com/dotnet/runtime/blob/2cc068d0008c898c67578f2868bd5b17a64c6366/src/coreclr/gc/gcconfig.h#L100
        // This is also our only option on .NET 7: GCHighMemPercent in GC.GetConfigurationVariables() always
        // reports 0 there, even when explicitly configured (dotnet/runtime#84198), so we group .NET 7 with .NET 6.
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

    [TestingAndPrivateOnly]
    internal static bool ReadHasConfiguredHeapHardLimit()
    {
        // Presence-only: we never parse these values (hex/octal env vars, percent vs. absolute, SOH+LOH+POH
        // summation), we only care whether *something* in the GCHeapHardLimit family is configured.
        try
        {
            if (EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHeapHardLimit) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHeapHardLimit) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHeapHardLimitPercent) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHeapHardLimitPercent) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHeapHardLimitSOH) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHeapHardLimitSOH) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHeapHardLimitLOH) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHeapHardLimitLOH) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHeapHardLimitPOH) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHeapHardLimitPOH) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHeapHardLimitSOHPercent) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHeapHardLimitSOHPercent) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHeapHardLimitLOHPercent) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHeapHardLimitLOHPercent) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetGCHeapHardLimitPOHPercent) is not null
             || EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ComPlusGCHeapHardLimitPOHPercent) is not null)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading configured GC heap hard limit");
        }

        try
        {
            // Presence-only fallback signal for pre-.NET 7, or when GC.GetConfigurationVariables() in unusable.
            // See gcconfig.h in src/coreclr/gc.
            return AppContext.GetData(PlatformKeys.AppContextGCHeapHardLimit) is string
                || AppContext.GetData(PlatformKeys.AppContextGCHeapHardLimitPercent) is string
                || AppContext.GetData(PlatformKeys.AppContextGCHeapHardLimitSOH) is string
                || AppContext.GetData(PlatformKeys.AppContextGCHeapHardLimitLOH) is string
                || AppContext.GetData(PlatformKeys.AppContextGCHeapHardLimitPOH) is string
                || AppContext.GetData(PlatformKeys.AppContextGCHeapHardLimitSOHPercent) is string
                || AppContext.GetData(PlatformKeys.AppContextGCHeapHardLimitLOHPercent) is string
                || AppContext.GetData(PlatformKeys.AppContextGCHeapHardLimitPOHPercent) is string;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error reading GC heap hard limit knobs from AppContext");
            // If there's an exception, fail "safe"
            return true;
        }
    }

    /// <summary>
    /// The GC memory configuration, either resolved authoritatively via <c>GC.GetConfigurationVariables()</c> (.NET
    /// 8+, plus the heap hard limit only on .NET 7) or inferred from configuration-knob presence checks.
    /// </summary>
    internal readonly struct GcMemoryConfiguration(int? highMemoryLoadThresholdPercent, bool hasConfiguredHighMemoryLoadPercent, bool hasHeapHardLimit)
    {
        /// <summary>
        /// Gets the GC's true "high memory load" threshold percentage (already clamped to 0-99), or <c>null</c> if
        /// it can't be determined reliably.
        /// </summary>
        public int? HighMemoryLoadThresholdPercent { get; } = highMemoryLoadThresholdPercent;

        /// <summary>
        /// Gets a value indicating whether a <c>GCHighMemPercent</c>-style override is configured. This can be true
        /// even when <see cref="HighMemoryLoadThresholdPercent"/> is null (.NET 6/7, where we can detect that
        /// *something* is configured via env vars/AppContext, but not resolve the actual value).
        /// </summary>
        public bool HasConfiguredHighMemoryLoadPercent { get; } = hasConfiguredHighMemoryLoadPercent;

        /// <summary>
        /// Gets a value indicating whether a GC heap hard limit is in play. Authoritative - including a
        /// container's implicit default - only when <c>GC.GetConfigurationVariables()</c> succeeded (.NET 7+, the
        /// common case). Otherwise (primarily .NET 6) this falls back to a best-effort presence
        /// check over <em>explicit</em> configuration knobs only: it cannot see a hard limit the runtime derives
        /// implicitly from a container/cgroup memory limit, since the runtime exposes no knob for that case.
        /// A false negative here lets <see cref="TryCalculate"/> take Route B and scale by <c>TotalAvailableMemoryBytes</c>
        /// as if it were <c>total_physical_mem</c>, when it may actually be a smaller, implicit <c>heap_hard_limit</c>
        /// (the runtime defaults it to 75% of the cgroup limit), leading to silently inflating/over-reporting
        /// the load percentage, rather than returning <c>null</c>.
        /// </summary>
        public bool HasHeapHardLimit { get; } = hasHeapHardLimit;
    }
}
#endif
