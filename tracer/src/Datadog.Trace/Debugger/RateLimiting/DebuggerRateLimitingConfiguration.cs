// <copyright file="DebuggerRateLimitingConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Configuration for debugger rate limiting and circuit breaker
    /// </summary>
    internal class DebuggerRateLimitingConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum debugger overhead budget expressed as a percentage of a single CPU core.
        /// This value controls how much additional wall-clock time per window is allowed to be spent in
        /// debugger probe execution before rate limiting and circuit breaking start to apply.
        ///
        /// Note: This is <b>not</b> a strict process-wide CPU percentage as seen in OS tools like top/Task Manager.
        /// On multi-core machines, the effective process share is approximately
        /// MaxGlobalCpuPercentage / Environment.ProcessorCount. For example, on an 8-core host,
        /// the default value of 1.5 corresponds to roughly 0.2% of the total CPU capacity.
        /// </summary>
        public double MaxGlobalCpuPercentage { get; set; } = 1.5;

        /// <summary>
        /// Gets or sets the duration of global budget window (default 1 second)
        /// </summary>
        public TimeSpan GlobalBudgetWindow { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the threshold for hot loop detection in hits/second (default 10000)
        /// </summary>
        public long HotLoopThresholdHitsPerSecond { get; set; } = 10_000;

        /// <summary>
        /// Gets or sets the maximum average cost per execution in microseconds (default 100μs)
        /// </summary>
        public long MaxAverageCostMicroseconds { get; set; } = 100;

        /// <summary>
        /// Gets or sets the number of consecutive exhausted windows before opening circuit (default 3)
        /// </summary>
        public int WindowsBeforeCircuitOpen { get; set; } = 3;

        /// <summary>
        /// Gets or sets the maximum burst multiplier for adaptive sampler (default 3)
        /// </summary>
        public int MaxPerWindowBurst { get; set; } = 3;

        /// <summary>
        /// Gets or sets the default samples per second for probes (default 1)
        /// </summary>
        public int DefaultSamplesPerSecond { get; set; } = 1;

        /// <summary>
        /// Gets or sets the average lookback for adaptive sampler EMA (default 180)
        /// </summary>
        public int AverageLookback { get; set; } = 180;

        /// <summary>
        /// Gets or sets the budget lookback for adaptive sampler (default 16)
        /// </summary>
        public int BudgetLookback { get; set; } = 16;

        /// <summary>
        /// Gets or sets a value indicating whether to enable enhanced rate limiting (default true)
        /// </summary>
        public bool EnableEnhancedRateLimiting { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable circuit breaker (default true)
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable thread-local prefilter (default true)
        /// </summary>
        public bool EnableThreadLocalPrefilter { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable memory pressure monitoring (default true)
        /// </summary>
        public bool EnableMemoryPressureMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets the memory pressure preset to use.
        /// Auto (default): Automatically detect environment and apply Standard or Constrained preset.
        /// Standard: 92% memory, 3 Gen2/sec - for typical servers.
        /// Constrained: 99% memory, 200 Gen2/sec - for serverless/low-resource environments.
        /// Aggressive: 87% memory, 1 Gen2/sec - for ultra-sensitive workloads (user opt-in).
        /// When set to non-Auto, overrides individual threshold settings unless they are explicitly configured (non-zero).
        /// </summary>
        public MemoryPreset MemoryPreset { get; set; } = MemoryPreset.Auto;

        /// <summary>
        /// Gets or sets the memory usage threshold above which high pressure is detected.
        /// Default: 0 (auto-detect based on MemoryPreset).
        /// When set to 0, the value is determined by MemoryPreset.
        /// Valid range: 0 (auto) or 1-100.
        /// </summary>
        public double MaxMemoryUsagePercent { get; set; } = 0;

        /// <summary>
        /// Gets or sets the memory usage threshold for starting degradation (default 0.70 = 70%)
        /// </summary>
        public double MemoryDegradationThreshold { get; set; } = 70.0;

        /// <summary>
        /// Gets or sets the maximum Gen2 collections per second before detecting memory pressure.
        /// Default: 0 (auto-detect based on MemoryPreset).
        /// When set to 0, the value is determined by MemoryPreset.
        /// Valid range: 0 (auto) or positive integer.
        /// </summary>
        public int MaxGen2CollectionsPerSecond { get; set; } = 0;

        /// <summary>
        /// Validates the configuration and throws if invalid
        /// </summary>
        public void Validate()
        {
            if (MaxGlobalCpuPercentage <= 0 || MaxGlobalCpuPercentage > 100)
            {
                throw new ArgumentException("MaxGlobalCpuPercentage must be between 0 and 100");
            }

            if (GlobalBudgetWindow <= TimeSpan.Zero)
            {
                throw new ArgumentException("GlobalBudgetWindow must be positive");
            }

            if (HotLoopThresholdHitsPerSecond <= 0)
            {
                throw new ArgumentException("HotLoopThresholdHitsPerSecond must be positive");
            }

            if (MaxAverageCostMicroseconds <= 0)
            {
                throw new ArgumentException("MaxAverageCostMicroseconds must be positive");
            }

            if (WindowsBeforeCircuitOpen <= 0)
            {
                throw new ArgumentException("WindowsBeforeCircuitOpen must be positive");
            }

            if (MaxPerWindowBurst <= 0)
            {
                throw new ArgumentException("MaxPerWindowBurst must be positive");
            }

            // MaxMemoryUsagePercent: 0 means auto, otherwise must be in valid range
            if (MaxMemoryUsagePercent < 0 || MaxMemoryUsagePercent > 100)
            {
                throw new ArgumentException("MaxMemoryUsagePercent must be between 0 (auto) and 100");
            }

            if (MemoryDegradationThreshold < 0 || MemoryDegradationThreshold > 100)
            {
                throw new ArgumentException("MemoryDegradationThreshold must be between 0 and 100");
            }

            // MaxGen2CollectionsPerSecond: 0 means auto, negative is invalid
            if (MaxGen2CollectionsPerSecond < 0)
            {
                throw new ArgumentException("MaxGen2CollectionsPerSecond must be 0 (auto) or positive");
            }
        }
    }
}
