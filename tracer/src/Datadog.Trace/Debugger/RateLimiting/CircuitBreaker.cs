// <copyright file="CircuitBreaker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Thread-safe circuit breaker implementation with exponential backoff for probe protection.
    /// Uses lock for state transitions and atomic operations for statistics tracking.
    /// Opens when:
    /// - Hit rate exceeds threshold
    /// - Average execution cost is too high
    /// - Global budget exhausted repeatedly
    /// - Memory pressure detected
    /// </summary>
    internal class CircuitBreaker : ICircuitBreaker, IDisposable
    {
        private const int MaxHalfOpenTrials = 10;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CircuitBreaker>();

        private readonly string _probeId;
        private readonly IGlobalBudget _globalBudget;
        private readonly IMemoryPressureMonitor? _memoryPressureMonitor;
        private readonly long _hotLoopThresholdHitsPerSecond;
        private readonly long _maxAverageCostTicks;
        private readonly int _windowsBeforeOpen;
        private readonly object _stateLock = new object();
        private readonly IDisposable _checkSubscription;

        // State machine fields - guarded by _stateLock
        // Using volatile for both atomicity and cross-thread visibility
        private volatile CircuitState _state;
        private long _stateTransitionTimestamp;
        private TimeSpan _backoffDuration;
        private int _halfOpenSuccessfulTrials;
        private int _halfOpenTotalTrials;

        // Statistics tracking - atomic for lock-free updates in hot path
        private long _totalHits;
        private long _totalCostTicks;
        private long _hotLoopMarker;
        private long _memoryPressureMarker;

        // Window tracking for statistics reset
        private long _lastResetTimestamp;

        private volatile bool _disposed;

        public CircuitBreaker(
            string probeId,
            IGlobalBudget globalBudget,
            IMemoryPressureMonitor? memoryPressureMonitor = null,
            long hotLoopThresholdHitsPerSecond = 10000,
            long maxAverageCostMicroseconds = 100,
            int windowsBeforeOpen = 3,
            ISamplerScheduler? scheduler = null)
        {
            _probeId = probeId ?? throw new ArgumentNullException(nameof(probeId));
            _globalBudget = globalBudget ?? throw new ArgumentNullException(nameof(globalBudget));
            _memoryPressureMonitor = memoryPressureMonitor;

            if (hotLoopThresholdHitsPerSecond <= 0)
            {
                throw new ArgumentException("Hot loop threshold must be positive", nameof(hotLoopThresholdHitsPerSecond));
            }

            if (maxAverageCostMicroseconds <= 0)
            {
                throw new ArgumentException("Max average cost must be positive", nameof(maxAverageCostMicroseconds));
            }

            if (windowsBeforeOpen <= 0)
            {
                throw new ArgumentException("Windows before open must be positive", nameof(windowsBeforeOpen));
            }

            _hotLoopThresholdHitsPerSecond = hotLoopThresholdHitsPerSecond;
            _maxAverageCostTicks = (long)(maxAverageCostMicroseconds * (Stopwatch.Frequency / 1_000_000.0));
            _windowsBeforeOpen = windowsBeforeOpen;

            _state = CircuitState.Closed;
            _stateTransitionTimestamp = Stopwatch.GetTimestamp();
            _backoffDuration = TimeSpan.FromSeconds(1);
            _lastResetTimestamp = Stopwatch.GetTimestamp();

            // Check for circuit opening every second using a timer, not on every hit
            // This moves the expensive checks out of the hot path
            var interval = TimeSpan.FromSeconds(1);
            if (scheduler is null)
            {
                _checkSubscription = new TimerSubscription(interval, () => CheckForOpen(null));
            }
            else
            {
                _checkSubscription = scheduler.Schedule(() => CheckForOpen(null), interval);
            }
        }

        public CircuitState State
        {
            get
            {
                // Lock-free read - volatile ensures visibility across threads
                return _state;
            }
        }

        private static TimeSpan GetElapsedTime(long startTimestamp)
        {
            var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            return TimeSpan.FromSeconds(elapsed / (double)Stopwatch.Frequency);
        }

        /// <summary>
        /// Checks if a request should be allowed through the circuit.
        /// This is the hot path - must be extremely fast.
        /// </summary>
        public bool ShouldAllow()
        {
            // Fast path: if closed, always allow
            // Using local copy to avoid multiple volatile reads
            var currentState = _state;

            if (currentState == CircuitState.Closed)
            {
                return true;
            }

            // State is Open or HalfOpen - need synchronization
            lock (_stateLock)
            {
                // Re-read state under lock (could have changed)
                currentState = _state;

                switch (currentState)
                {
                    case CircuitState.Closed:
                        return true;

                    case CircuitState.Open:
                        // Check if backoff period has elapsed
                        var elapsed = GetElapsedTime(_stateTransitionTimestamp);
                        if (elapsed >= _backoffDuration)
                        {
                            TransitionToHalfOpen_Locked();
                            return true;
                        }

                        return false;

                    case CircuitState.HalfOpen:
                        // Allow trial if we haven't reached the limit
                        if (_halfOpenTotalTrials < MaxHalfOpenTrials)
                        {
                            _halfOpenTotalTrials++;
                            return true;
                        }

                        return false;

                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Records a successful execution. Lock-free for performance.
        /// </summary>
        public void RecordSuccess(long elapsedTicks)
        {
            if (elapsedTicks < 0)
            {
                // Invalid measurement - ignore
                return;
            }

            // Update statistics atomically - this is the hot path
            Interlocked.Increment(ref _totalHits);
            Interlocked.Add(ref _totalCostTicks, elapsedTicks);

            // If we're in half-open state, track successful trials
            if (_state == CircuitState.HalfOpen)
            {
                lock (_stateLock)
                {
                    // Re-check state under lock
                    if (_state == CircuitState.HalfOpen)
                    {
                        _halfOpenSuccessfulTrials++;

                        // If all trials succeeded, transition to closed
                        if (_halfOpenSuccessfulTrials >= MaxHalfOpenTrials)
                        {
                            TransitionToClosed_Locked();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Records a failed execution.
        /// </summary>
        public void RecordFailure()
        {
            Interlocked.Increment(ref _totalHits);

            // If we're in half-open state, a single failure reopens the circuit
            if (_state == CircuitState.HalfOpen)
            {
                lock (_stateLock)
                {
                    // Re-check state under lock
                    if (_state == CircuitState.HalfOpen)
                    {
                        TransitionToOpen_Locked();
                    }
                }
            }
        }

        /// <summary>
        /// Marks this probe as being in a hot loop. Called externally when hot loop detected.
        /// </summary>
        public void RecordHotLoop()
        {
            // Set atomic marker
            Interlocked.Exchange(ref _hotLoopMarker, 1);
        }

        /// <summary>
        /// Marks that memory pressure is causing issues with this probe
        /// </summary>
        public void RecordMemoryPressure()
        {
            // Set atomic marker
            Interlocked.Exchange(ref _memoryPressureMarker, 1);
            _memoryPressureMonitor?.RecordMemoryPressureEvent();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _checkSubscription?.Dispose();
        }

        /// <summary>
        /// Periodic check for circuit opening conditions.
        /// Called by timer, not on every hit (moved out of hot path).
        /// </summary>
        private void CheckForOpen(object? state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // Only check if circuit is closed
                if (_state != CircuitState.Closed)
                {
                    return;
                }

                // Calculate time since last reset
                var now = Stopwatch.GetTimestamp();
                var lastReset = Interlocked.Read(ref _lastResetTimestamp);
                var elapsed = GetElapsedTime(lastReset);

                // Wait for at least 1 second of data
                if (elapsed.TotalSeconds < 1.0)
                {
                    return;
                }

                // Read and reset statistics atomically for next window.
                // NOTE: Exchange the timestamp first to bound the accounting window.
                // RecordSuccess() / RecordFailure() calls between the timestamp exchange and
                // the counter exchanges may be counted in either window. This is acceptable
                // for statistical tracking and avoids hot-path synchronization.
                Interlocked.Exchange(ref _lastResetTimestamp, now);
                var hits = Interlocked.Exchange(ref _totalHits, 0);
                var cost = Interlocked.Exchange(ref _totalCostTicks, 0);
                var hotLoopMarker = Interlocked.Exchange(ref _hotLoopMarker, 0);
                var memoryPressureMarker = Interlocked.Exchange(ref _memoryPressureMarker, 0);

                // Check conditions for opening circuit
                var denominator = Math.Max(1.0, elapsed.TotalSeconds);
                var hitsPerSecond = hits / denominator;
                var avgCost = hits > 0 ? cost / hits : 0;
                var consecutiveExhausted = _globalBudget.GetConsecutiveExhaustedWindows();

                bool shouldOpen = false;
                string? reason = null;

                if (hotLoopMarker == 1)
                {
                    shouldOpen = true;
                    reason = "hot loop detected";
                }
                else if (memoryPressureMarker == 1)
                {
                    shouldOpen = true;
                    reason = "memory pressure detected";
                }
                else if (hitsPerSecond > _hotLoopThresholdHitsPerSecond)
                {
                    shouldOpen = true;
                    reason = $"hit rate too high: {hitsPerSecond:F0}/sec";
                }
                else if (avgCost > _maxAverageCostTicks)
                {
                    shouldOpen = true;
                    var avgCostUs = avgCost / (Stopwatch.Frequency / 1_000_000.0);
                    reason = $"average cost too high: {avgCostUs:F0}μs";
                }
                else if (consecutiveExhausted >= _windowsBeforeOpen)
                {
                    shouldOpen = true;
                    reason = $"global budget exhausted {consecutiveExhausted} times";
                }

                bool opened = false;
                if (shouldOpen)
                {
                    lock (_stateLock)
                    {
                        // Re-check state under lock
                        if (_state == CircuitState.Closed)
                        {
                            TransitionToOpen_Locked();
                            opened = true;
                        }
                    }

                    if (opened)
                    {
                        Log.Warning("CircuitBreaker: Probe {ProbeId} opening: {Reason}", _probeId, reason);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never throw from timer callback
                Log.Error(ex, "CircuitBreaker: Error in CheckForOpen for probe {ProbeId}", _probeId);
            }
        }

        // State transition methods - must be called under _stateLock
        private void TransitionToOpen_Locked()
        {
            if (_state == CircuitState.Open)
            {
                return; // Already open
            }

            var previousState = _state;
            _state = CircuitState.Open;
            _stateTransitionTimestamp = Stopwatch.GetTimestamp();

            // Exponential backoff, capped at 60 seconds
            // Only double if transitioning from Closed or HalfOpen (not if already calculating backoff)
            if (previousState == CircuitState.Closed)
            {
                // First time opening - use initial backoff
                _backoffDuration = TimeSpan.FromSeconds(1);
            }
            else
            {
                // Reopening from HalfOpen - double the backoff
                var newBackoff = _backoffDuration.TotalSeconds * 2;
                _backoffDuration = TimeSpan.FromSeconds(Math.Min(newBackoff, 60));
            }

            Log.Information(
                "CircuitBreaker: Probe {ProbeId} opened, backoff={BackoffSec}s",
                _probeId,
                _backoffDuration.TotalSeconds);
        }

        private void TransitionToHalfOpen_Locked()
        {
            _state = CircuitState.HalfOpen;
            _stateTransitionTimestamp = Stopwatch.GetTimestamp();
            _halfOpenSuccessfulTrials = 0;
            _halfOpenTotalTrials = 0;

            Log.Information(
                "CircuitBreaker: Probe {ProbeId} half-open, allowing {Trials} trials",
                property0: _probeId,
                property1: MaxHalfOpenTrials);
        }

        private void TransitionToClosed_Locked()
        {
            _state = CircuitState.Closed;
            _stateTransitionTimestamp = Stopwatch.GetTimestamp();
            _backoffDuration = TimeSpan.FromSeconds(1); // Reset backoff

            // Reset all statistics
            Interlocked.Exchange(ref _totalHits, 0);
            Interlocked.Exchange(ref _totalCostTicks, 0);
            Interlocked.Exchange(ref _hotLoopMarker, 0);
            Interlocked.Exchange(ref _memoryPressureMarker, 0);
            Interlocked.Exchange(ref _lastResetTimestamp, Stopwatch.GetTimestamp());

            Log.Information("CircuitBreaker: Probe {ProbeId} closed, circuit recovered", _probeId);
        }

        private sealed class TimerSubscription : IDisposable
        {
            private readonly Timer _timer;

            public TimerSubscription(TimeSpan interval, Action callback)
            {
                _timer = new Timer(_ => callback(), null, interval, interval);
            }

            public void Dispose()
            {
                _timer.Dispose();
            }
        }
    }
}
