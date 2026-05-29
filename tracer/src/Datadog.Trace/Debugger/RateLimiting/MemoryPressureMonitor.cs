// <copyright file="MemoryPressureMonitor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>Reads the current process memory-load ratio (0-1+). Returns false when unsupported.</summary>
    internal delegate bool TryReadMemoryUsageRatio(out double ratio);

    /// <summary>Reads the cumulative gen2 collection count. Returns false when unsupported.</summary>
    internal delegate bool TryReadGen2CollectionCount(out int count);

    internal delegate void MemoryPressureTransitionHandler(
        bool isHighPressure,
        MetricTags.DebuggerMemoryPressureTrigger trigger,
        double? memoryUsagePercent,
        double? gen2CollectionsPerSecond,
        double highPressureDurationMs);

    /// <summary>
    /// Runtime memory pressure monitor for Dynamic Instrumentation.
    /// Monitors system memory usage and GC Gen2 collection frequency with debounce
    /// to avoid flapping between high and normal pressure states.
    /// This monitor is currently observational only: pressure transitions are reported via
    /// the transition callback (telemetry in production); the computed
    /// <see cref="IsHighMemoryPressure"/> state is not yet used to gate or throttle probe capture.
    /// </summary>
    /// <remarks>
    /// Concurrency model: the hot <see cref="RefreshIfStale()"/> fast path is lock-free (volatile reads only).
    /// The actual sampling/commit in <see cref="Refresh"/> is serialized by a non-blocking
    /// <c>_refreshInProgress</c> CAS so there is exactly one writer; every mutated field therefore needs no lock.
    /// Disposal is lock-free and best-effort: an in-flight refresh may publish one final, benign sample/telemetry
    /// transition concurrently with disposal. This is acceptable because the monitor is observational only and
    /// disposal happens at shutdown.
    /// </remarks>
    internal sealed class MemoryPressureMonitor : IDisposable
    {
        private const long DefaultRefreshIntervalMs = 1000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MemoryPressureMonitor>();
        private static readonly TryReadMemoryUsageRatio DefaultMemoryReader = SystemMemorySource.TryGetMemoryUsageRatio;
        private static readonly TryReadGen2CollectionCount DefaultGen2Reader = SystemMemorySource.TryGetGen2CollectionCount;
        private static readonly MemoryPressureTransitionHandler DefaultTransition = RecordTransitionTelemetry;
        private static readonly Action<MetricTags.DebuggerMemoryPressureDisabledReason> DefaultDisabled = RecordDisabledTelemetry;

        private readonly long _staleThresholdMs;
        private readonly double _memoryExitThreshold;
        private readonly int _gen2ExitThreshold;
        private readonly int _consecutiveHighToEnter;
        private readonly int _consecutiveLowToExit;
        private readonly TryReadMemoryUsageRatio _tryReadMemoryRatio;
        private readonly TryReadGen2CollectionCount _tryReadGen2;
        private readonly MemoryPressureTransitionHandler _onTransition;
        private readonly Action<MetricTags.DebuggerMemoryPressureDisabledReason> _onDisabled;

        // Refresh writes these fields through a single CAS-guarded writer; hot readers use volatile reads.
        private int _currentMemoryUsagePercentTenths;
        private int _gen2CollectionsPerSecondHundredths;
        private volatile bool _isHighPressure;
        private int _disabled;
        private int _disposed;
        private int _refreshInProgress;
        private volatile bool _hasRefreshed;

        // Single-writer statistics (only mutated inside the CAS-guarded Refresh).
        private long _lastGen2Count;
        private long _lastRefreshMs;
        private long _highPressureStartMs;
        private bool _hasHighPressureStart;
        private int _highStreak;
        private int _lowStreak;
        private bool _hasGen2Baseline;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryPressureMonitor"/> class using the real
        /// system memory/GC sources and telemetry transition reporting.
        /// </summary>
        /// <param name="config">Memory pressure configuration thresholds and debounce.</param>
        public MemoryPressureMonitor(MemoryPressureConfig config)
            : this(config, memoryRatioReader: null, gen2Reader: null, onTransition: null, onDisabled: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryPressureMonitor"/> class. The optional
        /// delegates exist so tests can supply deterministic memory/GC samples and observe transitions
        /// without real GC interactions; production passes <c>null</c> and the system defaults are used.
        /// </summary>
        internal MemoryPressureMonitor(
            MemoryPressureConfig config,
            TryReadMemoryUsageRatio? memoryRatioReader,
            TryReadGen2CollectionCount? gen2Reader,
            MemoryPressureTransitionHandler? onTransition,
            Action<MetricTags.DebuggerMemoryPressureDisabledReason>? onDisabled = null)
        {
            // Clamp non-sensical configuration to safe minimums. Negative thresholds would otherwise
            // make `value > threshold` true for any reading and leave the monitor permanently high.
            // Upper bounds are intentionally not enforced - callers can use ratios > 1.0 to effectively
            // disable memory-based detection (Gen2 still applies).
            HighPressureThreshold = Math.Max(0.0, config.HighPressureThresholdRatio);
            MaxGen2PerSecond = Math.Max(0, config.MaxGen2PerSecond);
            _memoryExitThreshold = Math.Max(0.0, HighPressureThreshold - Math.Max(0.0, config.MemoryExitMargin));
            _gen2ExitThreshold = Math.Max(0, MaxGen2PerSecond - Math.Max(0, config.Gen2ExitMargin));
            _consecutiveHighToEnter = Math.Max(1, config.ConsecutiveHighToEnter);
            _consecutiveLowToExit = Math.Max(1, config.ConsecutiveLowToExit);
            _staleThresholdMs = config.RefreshInterval > TimeSpan.Zero ? (long)config.RefreshInterval.TotalMilliseconds : DefaultRefreshIntervalMs;
            _tryReadMemoryRatio = memoryRatioReader ?? DefaultMemoryReader;
            _tryReadGen2 = gen2Reader ?? DefaultGen2Reader;
            _onTransition = onTransition ?? DefaultTransition;
            _onDisabled = onDisabled ?? DefaultDisabled;
        }

        public double Gen2CollectionsPerSecond
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _gen2CollectionsPerSecondHundredths) * 0.01;
        }

        public double MemoryUsagePercent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _currentMemoryUsagePercentTenths) * 0.1;
        }

        public bool IsHighMemoryPressure
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _isHighPressure;
        }

        public double HighPressureThreshold { get; }

        public int MaxGen2PerSecond { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RefreshIfStale()
        {
            // Read the cheap monotonic clock directly (no abstraction) to keep this hot,
            // per-probe-activity entry point as light as possible.
            RefreshIfStale(GetTimestampMs());
        }

        // Time is passed in so tests can drive staleness deterministically without a clock seam.
        internal void RefreshIfStale(long nowMs)
        {
            // Once disposed or self-disabled the monitor never samples again, so short-circuit early.
            if (IsDisposed() || IsDisabled())
            {
                return;
            }

            // Sampling is intentionally activity-driven so idle customer processes do not pay a
            // continuous timer tax just because Dynamic Instrumentation is enabled.
            // The first sample always runs; afterwards we throttle by the stale interval. A dedicated
            // flag (rather than a sentinel timestamp) avoids re-sampling when the monotonic clock legitimately reads 0.
            if (_hasRefreshed && !IsStale(nowMs, Volatile.Read(ref _lastRefreshMs)))
            {
                return;
            }

            Refresh(nowMs);
        }

        public void Dispose()
        {
            // Lock-free, best-effort: an in-flight refresh may publish one last benign sample. We simply
            // mark disposed and clear the published state; new refreshes short-circuit on IsDisposed().
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _isHighPressure = false;
            Volatile.Write(ref _currentMemoryUsagePercentTenths, 0);
            Volatile.Write(ref _gen2CollectionsPerSecondHundredths, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long GetTimestampMs()
        {
#if NETCOREAPP3_0_OR_GREATER
            return Environment.TickCount64;
#else
            // net461/netstandard2.0 lack TickCount64. A 1s cadence does not need high resolution, but
            // Environment.TickCount (32-bit) wraps every ~24.9 days, so use Stopwatch for a stable monotonic ms.
            return unchecked((long)(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0));
#endif
        }

        private static void RecordTransitionTelemetry(bool isHighPressure, MetricTags.DebuggerMemoryPressureTrigger trigger, double? memoryUsagePercent, double? gen2CollectionsPerSecond, double highPressureDurationMs)
        {
            var state = isHighPressure
                            ? MetricTags.DebuggerMemoryPressureState.Enter
                            : MetricTags.DebuggerMemoryPressureState.Exit;

            TelemetryFactory.Metrics.RecordCountDebuggerMemoryPressureTransitions(state, trigger);

            if (memoryUsagePercent.HasValue)
            {
                TelemetryFactory.Metrics.RecordDistributionSharedDebuggerMemoryPressureMemoryUsagePct(state, memoryUsagePercent.Value);
            }

            if (gen2CollectionsPerSecond.HasValue)
            {
                TelemetryFactory.Metrics.RecordDistributionSharedDebuggerMemoryPressureGen2PerSec(state, gen2CollectionsPerSecond.Value);
            }

            if (!isHighPressure)
            {
                TelemetryFactory.Metrics.RecordDistributionSharedDebuggerMemoryPressureDurationMs(highPressureDurationMs);
            }
        }

        private static void RecordDisabledTelemetry(MetricTags.DebuggerMemoryPressureDisabledReason reason)
        {
            TelemetryFactory.Metrics.RecordCountDebuggerMemoryPressureDisabled(reason);
        }

        private void Refresh(long nowMs)
        {
            // Ensure exactly one sampler/writer at a time. This is a non-blocking guard, not a lock:
            // a concurrent caller simply skips this refresh (best-effort, observational).
            if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                if (IsDisposed() || IsDisabled())
                {
                    return;
                }

                var memAvailable = _tryReadMemoryRatio(out var memRatio);
                var gcAvailable = _tryReadGen2(out var gen2Count);

                // Re-check after sampling (which may have blocked) so a Dispose() that ran in the meantime
                // wins: we publish nothing and fire no transition. This is a single volatile read, not a lock;
                // a Dispose() landing after this check is the accepted, benign race for an observational monitor.
                if (IsDisposed())
                {
                    return;
                }

                // If neither signal is available on this runtime/platform, stop sampling permanently.
                if (!memAvailable && !gcAvailable)
                {
                    if (DisableCore(MetricTags.DebuggerMemoryPressureDisabledReason.NoSignals))
                    {
                        Log.Debug("MemoryPressureMonitor disabled: no memory or GC info available on this runtime/platform.");
                    }

                    return;
                }

                // Establish Gen2 baseline if needed.
                if (!_hasGen2Baseline && gcAvailable)
                {
                    Volatile.Write(ref _lastRefreshMs, nowMs);
                    _lastGen2Count = gen2Count;
                    _hasGen2Baseline = true;
                }

                var elapsedMs = nowMs - _lastRefreshMs;

                double gen2PerSecond = 0;
                if (_hasGen2Baseline && gcAvailable)
                {
                    var delta = gen2Count - _lastGen2Count;
                    if (delta < 0)
                    {
                        delta = 0;
                    }

                    _lastGen2Count = gen2Count;
                    gen2PerSecond = elapsedMs > 0 ? delta * 1000.0 / elapsedMs : 0;
                }

                var aboveEnterMem = memAvailable && (memRatio > HighPressureThreshold);
                var aboveExitMem = memAvailable && (memRatio > _memoryExitThreshold);
                var aboveEnterGc = _hasGen2Baseline && gcAvailable && (gen2PerSecond > MaxGen2PerSecond);
                var aboveExitGc = _hasGen2Baseline && gcAvailable && (gen2PerSecond > _gen2ExitThreshold);

                var meetsHighNow = _isHighPressure
                                       ? (aboveExitMem || aboveExitGc)
                                       : (aboveEnterMem || aboveEnterGc);

                var nextHigh = ComputeNextHigh(meetsHighNow);
                double? usagePercent = memAvailable ? memRatio * 100 : null;
                double? sampledGen2PerSecond = _hasGen2Baseline && gcAvailable ? gen2PerSecond : null;

                // Only publish a gauge we actually sampled this cycle; keep the previous value instead of
                // clobbering it with 0 when a signal is unavailable on this particular refresh.
                if (usagePercent.HasValue)
                {
                    Volatile.Write(ref _currentMemoryUsagePercentTenths, ToScaledInt(usagePercent.Value, 10));
                }

                if (sampledGen2PerSecond.HasValue)
                {
                    Volatile.Write(ref _gen2CollectionsPerSecondHundredths, ToScaledInt(sampledGen2PerSecond.Value, 100));
                }

                var prev = _isHighPressure;
                _isHighPressure = nextHigh;
                Volatile.Write(ref _lastRefreshMs, nowMs);
                _hasRefreshed = true;

                if (nextHigh == prev)
                {
                    return;
                }

                double durationMs = 0;
                var trigger = MetricTags.DebuggerMemoryPressureTrigger.None;
                if (nextHigh)
                {
                    // Entry always coincides with a high-streak cycle, so at least one "above enter" signal is set.
                    trigger = aboveEnterMem && aboveEnterGc
                                  ? MetricTags.DebuggerMemoryPressureTrigger.Both
                                  : aboveEnterMem
                                      ? MetricTags.DebuggerMemoryPressureTrigger.Memory
                                      : MetricTags.DebuggerMemoryPressureTrigger.Gen2;
                    _highPressureStartMs = nowMs;
                    _hasHighPressureStart = true;
                }
                else
                {
                    durationMs = _hasHighPressureStart ? nowMs - _highPressureStartMs : 0;
                    _highPressureStartMs = 0;
                    _hasHighPressureStart = false;
                }

                // Keep transition emission inside the single-writer refresh so observers see serialized,
                // ordered state transitions. This only runs on enter/exit, not every refresh.
                _onTransition(nextHigh, trigger, usagePercent, sampledGen2PerSecond, durationMs);

                Log.Debug(
                    "Memory pressure {State}: Usage={Usage:F1}%, Gen2/sec={Gen2:F2}",
                    property0: nextHigh ? "ENTER" : "EXIT",
                    property1: usagePercent ?? 0,
                    property2: sampledGen2PerSecond ?? 0);
            }
            catch (Exception ex)
            {
                DisableCore(MetricTags.DebuggerMemoryPressureDisabledReason.Error);
                Log.Error(ex, "Error refreshing memory pressure");
            }
            finally
            {
                Volatile.Write(ref _refreshInProgress, 0);
            }

            bool ComputeNextHigh(bool meetsHighNow)
            {
                if (meetsHighNow)
                {
                    if (_highStreak < _consecutiveHighToEnter)
                    {
                        _highStreak++;
                    }

                    _lowStreak = 0;
                }
                else
                {
                    if (_lowStreak < _consecutiveLowToExit)
                    {
                        _lowStreak++;
                    }

                    _highStreak = 0;
                }

                return _isHighPressure
                           ? _lowStreak < _consecutiveLowToExit // stay high until enough low cycles
                           : _highStreak >= _consecutiveHighToEnter; // enter after enough high cycles
            }

            static int ToScaledInt(double value, int scale)
            {
                if (value <= 0)
                {
                    return 0;
                }

                var scaled = value * scale;
                return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsStale(long nowMs, long lastRefreshMs)
        {
            return (nowMs - lastRefreshMs) >= _staleThresholdMs;
        }

        // Only ever called from inside the CAS-guarded Refresh, so no synchronization is required.
        private bool DisableCore(MetricTags.DebuggerMemoryPressureDisabledReason reason)
        {
            if (IsDisposed() || IsDisabled())
            {
                return false;
            }

            Volatile.Write(ref _disabled, 1);
            _isHighPressure = false;
            Volatile.Write(ref _currentMemoryUsagePercentTenths, 0);
            Volatile.Write(ref _gen2CollectionsPerSecondHundredths, 0);
            _onDisabled(reason);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDisposed()
        {
            return Volatile.Read(ref _disposed) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDisabled()
        {
            return Volatile.Read(ref _disabled) != 0;
        }
    }
}
