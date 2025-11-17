// <copyright file="MemoryPreset.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Preset configurations for memory pressure monitoring thresholds
    /// </summary>
    internal enum MemoryPreset
    {
        /// <summary>
        /// Automatically detect and apply appropriate preset based on environment.
        /// Uses DefaultMemoryChecker to determine if environment is constrained.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Standard preset for typical server environments.
        /// MaxMemoryUsagePercent: 92% (90-95% range)
        /// MaxGen2CollectionsPerSecond: 3 (2-5 range)
        /// Balanced protection for normal workloads.
        /// </summary>
        Standard = 1,

        /// <summary>
        /// Constrained/serverless preset for low-resource environments.
        /// MaxMemoryUsagePercent: 99% (98-99% range)
        /// MaxGen2CollectionsPerSecond: 200 (50-200 range, or 1000 to effectively disable)
        /// Allows maximum memory utilization before triggering protection.
        /// </summary>
        Constrained = 2,

        /// <summary>
        /// Aggressive preset for ultra-low-latency or sensitive workloads.
        /// MaxMemoryUsagePercent: 87% (85-90% range)
        /// MaxGen2CollectionsPerSecond: 1 (1-2 range)
        /// Very conservative thresholds for maximum protection.
        /// User opt-in only (not auto-detected).
        /// </summary>
        Aggressive = 3
    }
}
