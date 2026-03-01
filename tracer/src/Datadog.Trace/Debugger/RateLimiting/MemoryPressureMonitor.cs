// <copyright file="MemoryPressureMonitor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Runtime memory pressure monitor for debugger probe protection.
    /// Monitors system memory usage and GC Gen2 collection frequency with debounce
    /// to avoid flapping between high and normal pressure states.
    /// </summary>
    internal sealed class MemoryPressureMonitor : IMemoryPressureMonitor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MemoryPressureMonitor>();
        private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromSeconds(1);

        private static readonly TimerCallback TimerCallback = static state =>
        {
            if (state is MemoryPressureMonitor @this)
            {
                @this.Refresh();
            }
        };

        // private readonly TimeSpan _refreshInterval;
        private readonly IDisposable? _refreshTimer;
        private readonly object _lock = new object();
        private readonly double _memoryExitMargin;
        private readonly int _gen2ExitMargin;
        private readonly int _consecutiveHighToEnter;
        private readonly int _consecutiveLowToExit;
        private readonly IGCInfoProvider _gcInfoProvider;
        private readonly IHighResolutionClock _clock;

        // These fields are using inside a lock or with volatile read\write
        private double _currentMemoryUsagePercent = 0;
        private double _gen2CollectionsPerSecond = 0;
        private volatile bool _isHighPressure = false;
        private volatile bool _disabled = false;
        private volatile bool _disposed = false;

        // Statistics tracking
        private long _lastGen2Count = 0;
        private long _lastRefreshTimestamp;
        private long _pressureEventCount = 0;
        private int _highStreak = 0;
        private int _lowStreak = 0;
        private bool _hasGen2Baseline = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryPressureMonitor"/> class.
        /// </summary>
        /// <param name="config">Memory pressure config:
        /// Memory usage ratio (0.0-1.0) to trigger high pressure. Default: 0.85 (85%)
        /// Maximum Gen2 collections per second before triggering high pressure. Default: 2
        /// Margin below threshold before exiting high pressure. Default: 0.05 (5%)
        /// Margin below maxGen2PerSecond before exiting high pressure. Default: 1
        /// Number of consecutive high cycles required to enter high pressure. Default: 1
        /// Number of consecutive low cycles required to exit high pressure. Default: 1
        /// </param>
        /// <param name="scheduler">Optional custom scheduler. If null, uses Timer</param>
        /// <param name="gcInfoProvider">Optional GC info provider. If null, uses <see cref="SystemGCInfoProvider"/></param>
        /// <param name="clock">Optional clock. If null, uses <see cref="SystemClock"/></param>
        /// <remarks>
        /// Pressure events bypass consecutive cycle requirements and enter high pressure immediately.
        /// </remarks>
        public MemoryPressureMonitor(
            MemoryPressureConfig config,
            ISamplerScheduler? scheduler = null,
            IGCInfoProvider? gcInfoProvider = null,
            IHighResolutionClock? clock = null)
        {
            HighPressureThreshold = config.HighPressureThresholdRatio;
            MaxGen2PerSecond = config.MaxGen2PerSecond;
            _memoryExitMargin = config.MemoryExitMargin;
            _gen2ExitMargin = config.Gen2ExitMargin;
            _consecutiveHighToEnter = Math.Max(1, config.ConsecutiveHighToEnter);
            _consecutiveLowToExit = Math.Max(1, config.ConsecutiveLowToExit);
            _gcInfoProvider = gcInfoProvider ?? new SystemGCInfoProvider();
            _clock = clock ?? new SystemClock();
            _lastRefreshTimestamp = _clock.GetTimestamp();
            _lastGen2Count = 0; // Defer baseline to first Refresh() call
            _hasGen2Baseline = false;

            _refreshTimer = scheduler != null
                                ? scheduler.Schedule(Refresh, config.RefreshInterval)
                                : new Timer(TimerCallback, this, config.RefreshInterval, config.RefreshInterval);

            Refresh();
        }

        public double Gen2CollectionsPerSecond => Volatile.Read(ref _gen2CollectionsPerSecond);

        public double MemoryUsagePercent => Volatile.Read(ref _currentMemoryUsagePercent);

        public bool IsHighMemoryPressure => _isHighPressure;

        public double HighPressureThreshold { get; }

        public int MaxGen2PerSecond { get; }

        public void RecordMemoryPressureEvent()
        {
            Interlocked.Increment(ref _pressureEventCount);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _refreshTimer?.Dispose();
        }

        private void Refresh()
        {
            if (_disposed || _disabled)
            {
                return;
            }

            try
            {
                var now = _clock.GetTimestamp();

                // 1) Read
                double memRatio = 0;
                var memAvailable = true;
                try
                {
                    memRatio = _gcInfoProvider.GetMemoryUsageRatio();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to get memory usage ratio");
                    memAvailable = false;
                }

                var gen2Count = 0;
                var gcAvailable = true;
                try
                {
                    gen2Count = _gcInfoProvider.GetGen2CollectionCount();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to get Gen2 collection count");
                    gcAvailable = false;
                }

                var pressureEvents = Interlocked.Exchange(ref _pressureEventCount, 0);

                // 2) If nothing is available, disable the monitor
                if (!memAvailable && !gcAvailable)
                {
                    lock (_lock)
                    {
                        _isHighPressure = false;
                        Volatile.Write(ref _currentMemoryUsagePercent, 0);
                        Volatile.Write(ref _gen2CollectionsPerSecond, 0);
                        _disabled = true;
                        _refreshTimer?.Dispose();
                    }

                    Log.Debug("MemoryPressureMonitor disabled: no memory or GC info available on this runtime/platform.");
                    return;
                }

                // 3) Establish Gen2 baseline if needed
                if (!_hasGen2Baseline && gcAvailable)
                {
                    lock (_lock)
                    {
                        _lastRefreshTimestamp = now;
                        _lastGen2Count = gen2Count;
                        _hasGen2Baseline = true;
                    }
                }

                // 4) Compute rates and update state
                var exitMemThreshold = Math.Max(0, HighPressureThreshold - _memoryExitMargin);
                var exitGen2Threshold = Math.Max(0, MaxGen2PerSecond - _gen2ExitMargin);

                bool transition;
                bool newHigh;
                double logUsagePercent;
                double logGen2PerSecond;

                lock (_lock)
                {
                    var elapsedSeconds = (now - _lastRefreshTimestamp) / _clock.Frequency;

                    double gen2PerSecond = 0;
                    if (_hasGen2Baseline && gcAvailable)
                    {
                        var delta = gen2Count - _lastGen2Count;
                        if (delta < 0)
                        {
                            delta = 0;
                        }

                        _lastGen2Count = gen2Count;
                        gen2PerSecond = elapsedSeconds > 0 ? delta / elapsedSeconds : 0;
                    }

                    // Threshold checks
                    var hasEvent = pressureEvents > 0;
                    var aboveEnterMem = memAvailable && (memRatio > HighPressureThreshold);
                    var aboveExitMem = memAvailable && (memRatio > exitMemThreshold);
                    var aboveEnterGc = _hasGen2Baseline && gcAvailable && (gen2PerSecond > MaxGen2PerSecond);
                    var aboveExitGc = _hasGen2Baseline && gcAvailable && (gen2PerSecond > exitGen2Threshold);

                    var meetsHighNow = _isHighPressure
                                           ? (aboveExitMem || aboveExitGc || hasEvent)
                                           : (aboveEnterMem || aboveEnterGc || hasEvent);

                    bool nextHigh = ComputeNextHigh(meetsHighNow, hasEvent);

                    Volatile.Write(ref _currentMemoryUsagePercent, memRatio * 100);
                    Volatile.Write(ref _gen2CollectionsPerSecond, gen2PerSecond);

                    var prev = _isHighPressure;
                    _isHighPressure = nextHigh;
                    _lastRefreshTimestamp = now;

                    transition = nextHigh != prev;
                    newHigh = nextHigh;
                    logUsagePercent = _currentMemoryUsagePercent;
                    logGen2PerSecond = _gen2CollectionsPerSecond;
                }

                if (transition)
                {
                    Log.Debug(
                        "Memory pressure {State}: Usage={Usage:F1}%, Gen2/sec={Gen2:F2}",
                        property0: newHigh ? "ENTER" : "EXIT",
                        property1: logUsagePercent,
                        property2: logGen2PerSecond);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing memory pressure");
            }

            bool ComputeNextHigh(bool meetsHighNow, bool hasEvent)
            {
                if (hasEvent)
                {
                    _highStreak = _consecutiveHighToEnter;
                    _lowStreak = 0;
                    return true;
                }

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
        }
    }
}
