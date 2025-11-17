// <copyright file="ProbeRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal class ProbeRateLimiter : IDisposable
    {
        private const int DefaultSamplesPerSecond = 1;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeRateLimiter));

        private static object _globalInstanceLock = new();
        private static bool _globalInstanceInitialized;
        private static ProbeRateLimiter? _instance;

        // Allow custom configuration to be set before first access to Instance
        private static DebuggerRateLimitingConfiguration? _globalConfiguration;

        private readonly ConcurrentDictionary<string, IAdaptiveSampler> _samplers = new();
        private readonly ConcurrentDictionary<string, ProtectedSampler> _protectedSamplers = new();
        private readonly DebuggerRateLimitingConfiguration _configuration;
        private readonly IGlobalBudget _globalBudget;
        private readonly IMemoryPressureMonitor? _memoryPressureMonitor;
        private readonly ISamplerScheduler _scheduler;
        private readonly Timer? _budgetMonitorTimer;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbeRateLimiter"/> class with default configuration.
        /// For custom configuration, use the constructor overload or call <see cref="ConfigureGlobalInstance"/>.
        /// </summary>
        public ProbeRateLimiter()
            : this(_globalConfiguration ?? new DebuggerRateLimitingConfiguration())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbeRateLimiter"/> class with custom configuration.
        /// </summary>
        /// <param name="configuration">The rate limiting configuration to use</param>
        public ProbeRateLimiter(DebuggerRateLimitingConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            configuration.Validate();
            _configuration = configuration;
            _globalBudget = new GlobalBudget(
                configuration.MaxGlobalCpuPercentage,
                configuration.GlobalBudgetWindow);

            _scheduler = new SharedSamplerScheduler();

            // Initialize memory pressure monitor if enabled
            if (configuration.EnableMemoryPressureMonitoring)
            {
                var memoryConfig = ResolveMemoryConfig(configuration);
                _memoryPressureMonitor = new MemoryPressureMonitor(memoryConfig, scheduler: _scheduler);
            }

            // Monitor global budget and adjust thread-local prefilter
            if (configuration.EnableThreadLocalPrefilter)
            {
                _budgetMonitorTimer = new Timer(
                    MonitorGlobalBudget,
                    null,
                    configuration.GlobalBudgetWindow,
                    configuration.GlobalBudgetWindow);
            }

            Log.Information(
                "ProbeRateLimiter initialized: MaxCPU={MaxCpu}%, HotLoopThreshold={HotLoop}/s",
                property0: configuration.MaxGlobalCpuPercentage,
                property1: configuration.HotLoopThresholdHitsPerSecond);
        }

        /// <summary>
        /// Gets the global singleton instance of ProbeRateLimiter.
        /// Uses default configuration unless <see cref="ConfigureGlobalInstance"/> was called first.
        /// Note: The singleton always uses the configuration set at first access.
        /// Subsequent calls to ConfigureGlobalInstance will have no effect.
        /// </summary>
        internal static ProbeRateLimiter Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock)!;
            }
        }

        /// <summary>
        /// Gets the global budget for monitoring and testing
        /// </summary>
        internal IGlobalBudget GlobalBudget => _globalBudget;

        /// <summary>
        /// Gets the memory pressure monitor for testing and diagnostics
        /// </summary>
        internal IMemoryPressureMonitor? MemoryPressureMonitor => _memoryPressureMonitor;

        /// <summary>
        /// Configures the global singleton instance before first access.
        /// Must be called before accessing <see cref="Instance"/> for the first time.
        /// If Instance has already been accessed, this method throws InvalidOperationException.
        /// </summary>
        /// <param name="configuration">The configuration to use for the global instance</param>
        /// <exception cref="InvalidOperationException">Thrown if Instance was already accessed</exception>
        public static void ConfigureGlobalInstance(DebuggerRateLimitingConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            lock (_globalInstanceLock)
            {
                if (_globalInstanceInitialized)
                {
                    throw new InvalidOperationException(
                        "Cannot configure global instance after it has been initialized. " +
                        "ConfigureGlobalInstance must be called before first access to Instance property.");
                }

                configuration.Validate();
                _globalConfiguration = configuration;
            }
        }

        /// <summary>
        /// Resolves memory config based on configuration preset and explicit values.
        /// Auto-detects environment characteristics when preset is Auto.
        /// </summary>
        private static MemoryPressureConfig ResolveMemoryConfig(DebuggerRateLimitingConfiguration configuration)
        {
            var memoryPercent = configuration.MaxMemoryUsagePercent;
            var maxGen2 = configuration.MaxGen2CollectionsPerSecond;
            var preset = configuration.MemoryPreset;

            var memoryExplicit = memoryPercent > 0;
            var gen2Explicit = maxGen2 > 0;

            var effectivePreset = preset;
            if (preset == MemoryPreset.Auto)
            {
                effectivePreset = DetectEnvironmentPreset();
                Log.Debug(
                    "Auto-detected memory preset: {Preset} (IsLowResource={IsLowResource})",
                    property0: effectivePreset,
                    property1: Caching.DefaultMemoryChecker.Instance.IsLowResourceEnvironment);
            }

            var defaults = GetPresetDefaults(effectivePreset);

            if (!memoryExplicit)
            {
                memoryPercent = defaults.MemoryPercent;
            }

            if (!gen2Explicit)
            {
                maxGen2 = defaults.MaxGen2PerSecond;
            }

            var baseConfig = MemoryPressureConfig.Default;
            var config = baseConfig with
            {
                HighPressureThresholdRatio = memoryPercent / 100.0,
                MaxGen2PerSecond = maxGen2
            };

            Log.Information(
                "Memory pressure config initialized: Preset={Preset}, Config={Config}",
                property0: effectivePreset,
                property1: config);

            return config;
        }

        /// <summary>
        /// Detects the appropriate memory preset based on environment characteristics.
        /// Uses DefaultMemoryChecker to assess resource availability.
        /// </summary>
        private static MemoryPreset DetectEnvironmentPreset()
        {
            try
            {
                // Check if we're in a low-resource environment (serverless, containers with tight limits, etc.)
                var isLowResource = Caching.DefaultMemoryChecker.Instance.IsLowResourceEnvironment;

                // Additional refinement: check GC available memory (if available)
#if NETCOREAPP3_1_OR_GREATER
                try
                {
                    var gcInfo = GC.GetGCMemoryInfo();
                    var availableGB = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);

                    // If available memory is very small (< 2 GB), treat as constrained
                    // even if DefaultMemoryChecker didn't flag it
                    if (availableGB < 2.0)
                    {
                        Log.Debug(
                            "Detected constrained environment: Available memory {AvailableGB:F2} GB < 2 GB threshold",
                            property: availableGB);
                        return MemoryPreset.Constrained;
                    }
                }
                catch
                {
                    // GC memory info not available or failed, rely on DefaultMemoryChecker
                }
#endif

                return isLowResource ? MemoryPreset.Constrained : MemoryPreset.Standard;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to detect environment preset, defaulting to Standard");
                return MemoryPreset.Standard;
            }
        }

        /// <summary>
        /// Gets the default memory and Gen2 thresholds for a given preset.
        /// </summary>
        private static MemoryThresholds GetPresetDefaults(MemoryPreset preset)
        {
            return preset switch
            {
                MemoryPreset.Standard => new MemoryThresholds(memoryPercent: 92.0, maxGen2PerSecond: 3, preset), // Balanced for typical servers
                MemoryPreset.Constrained => new MemoryThresholds(memoryPercent: 99.0, maxGen2PerSecond: 200, preset), // Maximum utilization for serverless/containers
                MemoryPreset.Aggressive => new MemoryThresholds(memoryPercent: 87.0, maxGen2PerSecond: 1, preset), // Conservative for sensitive workloads
                _ => new MemoryThresholds(memoryPercent: 92.0, maxGen2PerSecond: 3, preset) // Default to Standard for unknown
            };
        }

        public IAdaptiveSampler GetOrAddSampler(string probeId)
        {
            if (_configuration.EnableEnhancedRateLimiting)
            {
                return _protectedSamplers.GetOrAdd(probeId, probeId => CreateProtectedSampler(probeId));
            }

            return _samplers.GetOrAdd(probeId, _ => CreateSimpleSampler(DefaultSamplesPerSecond));
        }

        public bool TryAddSampler(string probeId, IAdaptiveSampler sampler)
        {
            return _samplers.TryAdd(probeId, sampler);
        }

        public void SetRate(string probeId, int samplesPerSecond)
        {
            // We currently don't support updating the probe rate limit, and that is fine
            // since the functionality in the UI is not exposed yet.
            if (_samplers.TryGetValue(probeId, out _) || _protectedSamplers.TryGetValue(probeId, out _))
            {
                Log.Information("Adaptive sampler already exist for {ProbeID}", probeId);
                return;
            }

            if (_configuration.EnableEnhancedRateLimiting)
            {
                var protectedSampler = CreateProtectedSampler(probeId, samplesPerSecond);
                if (!_protectedSamplers.TryAdd(probeId, protectedSampler))
                {
                    Log.Information("Protected sampler already exist for {ProbeID}", probeId);
                    protectedSampler.Dispose();
                }
            }
            else
            {
                var adaptiveSampler = CreateSimpleSampler(samplesPerSecond);
                if (!_samplers.TryAdd(probeId, adaptiveSampler))
                {
                    Log.Information("Adaptive sampler already exist for {ProbeID}", probeId);
                }
            }
        }

        public void ResetRate(string probeId)
        {
            if (_samplers.TryRemove(probeId, out _))
            {
                Log.Debug("Removed simple sampler for {ProbeID}", probeId);
            }

            if (_protectedSamplers.TryRemove(probeId, out var protectedSampler))
            {
                protectedSampler.Dispose();
                Log.Debug("Removed protected sampler for {ProbeID}", probeId);
            }
        }

        /// <summary>
        /// Enables or disables the global kill switch for all probes
        /// </summary>
        public void SetKillSwitch(bool enabled)
        {
            foreach (var sampler in _protectedSamplers.Values)
            {
                sampler.KillSwitch = enabled;
            }

            Log.Warning("Global kill switch {Status}", enabled ? "ENABLED" : "DISABLED");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _budgetMonitorTimer?.Dispose();
            _memoryPressureMonitor?.Dispose();
            ((IDisposable)_scheduler)?.Dispose();
            (_globalBudget as IDisposable)?.Dispose();

            foreach (var sampler in _protectedSamplers.Values)
            {
                sampler.Dispose();
            }

            _samplers.Clear();
            _protectedSamplers.Clear();
        }

        private AdaptiveSampler CreateSimpleSampler(int samplesPerSecond)
        {
            return new AdaptiveSampler(
                TimeSpan.FromSeconds(1),
                samplesPerSecond,
                _configuration.AverageLookback,
                _configuration.BudgetLookback,
                rollWindowCallback: null,
                scheduler: _scheduler,
                maxPerWindowBurst: _configuration.MaxPerWindowBurst);
        }

        private ProtectedSampler CreateProtectedSampler(string probeId, int samplesPerSecond = DefaultSamplesPerSecond)
        {
            var innerSampler = CreateSimpleSampler(samplesPerSecond);

            var circuitBreaker = _configuration.EnableCircuitBreaker
                ? new CircuitBreaker(
                    probeId,
                    _globalBudget,
                    _memoryPressureMonitor,
                    _configuration.HotLoopThresholdHitsPerSecond,
                    _configuration.MaxAverageCostMicroseconds,
                    _configuration.WindowsBeforeCircuitOpen,
                    scheduler: _scheduler)
                : (ICircuitBreaker)new NopCircuitBreaker();

            return new ProtectedSampler(
                probeId,
                innerSampler,
                circuitBreaker,
                _globalBudget,
                _memoryPressureMonitor,
                memoryDegradationThresholdPercent: _configuration.MemoryDegradationThreshold);
        }

        private void MonitorGlobalBudget(object? state)
        {
            try
            {
                if (_configuration.EnableThreadLocalPrefilter)
                {
                    var usage = _globalBudget.GetUsagePercentage();
                    var isExhausted = _globalBudget.IsExhausted;
                    ThreadLocalPrefilter.AdjustForPressure(usage, isExhausted);

                    if (usage > 50)
                    {
                        Log.Debug(
                            "Global budget usage: {Usage}%, Exhausted={Exhausted}, PrefilterMask={Mask}",
                            property0: usage,
                            property1: isExhausted,
                            property2: ThreadLocalPrefilter.GetFilterMask());
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ProbeRateLimiter: Error monitoring global budget");
            }
        }

        /// <summary>
        /// Container for resolved memory thresholds with metadata about which preset was used
        /// </summary>
        private readonly struct MemoryThresholds
        {
            public MemoryThresholds(double memoryPercent, int maxGen2PerSecond, MemoryPreset presetUsed)
            {
                MemoryPercent = memoryPercent;
                MaxGen2PerSecond = maxGen2PerSecond;
                PresetUsed = presetUsed;
            }

            public double MemoryPercent { get; }

            public int MaxGen2PerSecond { get; }

            public MemoryPreset PresetUsed { get; }
        }

        private class NopCircuitBreaker : ICircuitBreaker
        {
            public CircuitState State => CircuitState.Closed;

            public bool ShouldAllow() => true;

            public void RecordSuccess(long elapsedTicks)
            {
            }

            public void RecordFailure()
            {
            }

            public void RecordHotLoop()
            {
            }

            public void RecordMemoryPressure()
            {
            }
        }
    }
}
