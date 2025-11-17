// <copyright file="GlobalBudget.cs" company="Datadog">
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
    /// Manages a global CPU/time budget to prevent debugger from consuming excessive process resources.
    /// Thread-safe and lock-free for hot path operations. Uses atomic operations for all state updates.
    /// </summary>
    internal class GlobalBudget : IGlobalBudget, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<GlobalBudget>();

        private readonly long _maxBudgetTicksPerWindow;
        private readonly TimeSpan _windowDuration;
        private Timer? _resetTimer;

        // Atomic fields for thread-safe access
        private long _usedTicks;
        private int _consecutiveExhaustedWindows;

        // Exhaustion state: 0 = not exhausted, 1 = exhausted
        // Using int for atomic CompareExchange operations
        // Not marked volatile - use Volatile.Read/Write for access
        private int _exhaustionState;

        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalBudget"/> class.
        /// </summary>
        /// <param name="maxCpuPercentage">Maximum CPU percentage allowed (default 1.5%)</param>
        /// <param name="windowDuration">Duration of budget window (default 1 second)</param>
        public GlobalBudget(double maxCpuPercentage = 1.5, TimeSpan? windowDuration = null)
        {
            if (maxCpuPercentage <= 0 || maxCpuPercentage > 100)
            {
                throw new ArgumentException("CPU percentage must be between 0 and 100", nameof(maxCpuPercentage));
            }

            _windowDuration = windowDuration ?? TimeSpan.FromSeconds(1);

            if (_windowDuration <= TimeSpan.Zero)
            {
                throw new ArgumentException("Window duration must be positive", nameof(windowDuration));
            }

            // Calculate max ticks allowed per window
            // Stopwatch.Frequency gives ticks per second
            // For 1.5% CPU: maxTicks = (Frequency * windowSeconds * 0.015)
            var windowSeconds = _windowDuration.TotalSeconds;
            _maxBudgetTicksPerWindow = (long)(Stopwatch.Frequency * windowSeconds * (maxCpuPercentage / 100.0));

            if (_maxBudgetTicksPerWindow <= 0)
            {
                throw new ArgumentException("Calculated max budget ticks is invalid. Check CPU percentage and window duration.", nameof(maxCpuPercentage));
            }

            // Initialize timer with Timeout.Infinite, then start it after all fields are initialized
            // This prevents timer callback from firing during construction
            _resetTimer = new Timer(ResetWindow, null, Timeout.Infinite, Timeout.Infinite);

            // Now start the timer - all fields are initialized
            _resetTimer.Change(_windowDuration, _windowDuration);

            Log.Information(
                "GlobalBudget initialized: MaxCPU={MaxCpuPercentage}%, Window={WindowMs}ms, MaxTicks={MaxTicks}",
                maxCpuPercentage,
                _windowDuration.TotalMilliseconds,
                _maxBudgetTicksPerWindow);
        }

        /// <summary>
        /// Gets a value indicating whether the budget is currently exhausted.
        /// This is a volatile read - safe to call from any thread without synchronization.
        /// </summary>
        public bool IsExhausted => Volatile.Read(ref _exhaustionState) == 1;

        /// <summary>
        /// Records CPU usage. Thread-safe and lock-free.
        /// Uses atomic operations to ensure correctness under high concurrency.
        /// </summary>
        /// <param name="elapsedTicks">The elapsed CPU ticks to record</param>
        public void RecordUsage(long elapsedTicks)
        {
            if (elapsedTicks < 0)
            {
                // Negative ticks indicate measurement error - ignore
                return;
            }

            // Atomically add ticks and get new total
            var newUsed = Interlocked.Add(ref _usedTicks, elapsedTicks);

            // Check if we've exhausted the budget and transition state atomically
            // Only one thread will successfully transition from 0 to 1 and log the warning
            if (newUsed >= _maxBudgetTicksPerWindow)
            {
                // CompareExchange: if current value is 0, set to 1 and return 0
                // If current value is already 1, return 1 and don't change it
                var previousState = Interlocked.CompareExchange(ref _exhaustionState, 1, 0);

                if (previousState == 0)
                {
                    // We were the thread that transitioned to exhausted - log it once
                    Log.Warning(
                        "GlobalBudget exhausted: Used={UsedTicks}, Max={MaxTicks}, Usage={UsagePercent}%",
                        newUsed,
                        _maxBudgetTicksPerWindow,
                        GetUsagePercentage());
                }
            }
        }

        /// <summary>
        /// Gets the current usage as a percentage of the maximum budget.
        /// Thread-safe - uses atomic read.
        /// </summary>
        public double GetUsagePercentage()
        {
            var used = Interlocked.Read(ref _usedTicks);
            return _maxBudgetTicksPerWindow > 0
                ? (used / (double)_maxBudgetTicksPerWindow) * 100.0
                : 0.0;
        }

        /// <summary>
        /// Gets the number of consecutive windows where the budget was exhausted.
        /// Thread-safe - uses atomic read.
        /// </summary>
        public int GetConsecutiveExhaustedWindows()
        {
            return Interlocked.CompareExchange(ref _consecutiveExhaustedWindows, 0, 0);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Dispose the timer and wait for any executing callback to complete
            // Using Dispose() instead of Dispose(WaitHandle) for simplicity -
            // callbacks are protected by _disposed flag
            _resetTimer?.Dispose();
            _resetTimer = null;
        }

        private void ResetWindow(object? state)
        {
            // Check if disposed - timer callback could race with Dispose()
            if (_disposed)
            {
                return;
            }

            try
            {
                // Snapshot-and-reset window atomically for consistency.
                // We reset counters first, then compute based on the snapshot values.
                // This avoids racing with concurrent RecordUsage updates between separate reads.
                var used = Interlocked.Exchange(ref _usedTicks, 0);
                var wasExhausted = Interlocked.Exchange(ref _exhaustionState, 0);

                // Update consecutive exhausted counter
                if (wasExhausted == 1)
                {
                    Interlocked.Increment(ref _consecutiveExhaustedWindows);

                    if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
                    {
                        Log.Debug(
                            "Budget window reset (was exhausted): ConsecutiveExhausted={Count}, PrevUsage={Usage}%",
                            _consecutiveExhaustedWindows,
                            (used / (double)_maxBudgetTicksPerWindow) * 100.0);
                    }
                }
                else
                {
                    // Reset consecutive exhausted counter to 0
                    Interlocked.Exchange(ref _consecutiveExhaustedWindows, 0);
                }
            }
            catch (Exception ex)
            {
                // Never throw from timer callback - could terminate process
                Log.Error(ex, "GlobalBudget: Failed to reset window");
            }
        }
    }
}
