using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class RateLimiter : IRateLimiter
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<RateLimiter>();

        private readonly ManualResetEventSlim _refreshEvent = new ManualResetEventSlim(initialState: false);

        private readonly int _maxTracesPerInterval;
        private readonly int _intervalMilliseconds;

        private DateTime _windowBegin = DateTime.Now;
        private DateTime _lastRefresh = DateTime.Now;
        private ConcurrentQueue<ulong> _allowedTracesForInterval = new ConcurrentQueue<ulong>();

        private int _windowChecks = 0;
        private int _windowAllowed = 0;

        private int _previousWindowChecks = 0;
        private int _previousWindowAllowed = 0;

        public RateLimiter(int? maxTracesPerInterval)
        {
            _maxTracesPerInterval = maxTracesPerInterval ?? 100;
            _intervalMilliseconds = 1_000;
            Log.Debug("Allowing {0} p1 traces per {1} milliseconds.", _maxTracesPerInterval, _intervalMilliseconds);
        }

        public bool Allowed(ulong traceId)
        {
            if (_maxTracesPerInterval == 0)
            {
                // Rate limit of 0 blocks everything
                return false;
            }

            if (_maxTracesPerInterval < 0)
            {
                // Negative rate limit disables rate limiting
                return true;
            }

            WaitForRefresh();

            // This must happen after the wait, because we check for window statistics, modifying this number
            Interlocked.Increment(ref _windowChecks);

            if (_allowedTracesForInterval.Count >= _maxTracesPerInterval)
            {
                Log.Debug("Dropping trace id {0} with count of {1} for current window.", traceId, _allowedTracesForInterval.Count);
                return false;
            }

            _allowedTracesForInterval.Enqueue(traceId);

            Interlocked.Increment(ref _windowAllowed);

            return true;
        }

        public float GetEffectiveRate()
        {
            if (_maxTracesPerInterval == 0)
            {
                // Rate limit of 0 blocks everything
                return 0;
            }

            if (_maxTracesPerInterval < 0)
            {
                // Negative rate limit disables rate limiting
                return 1;
            }

            var totalChecksForLastTwoWindows = _windowChecks + _previousWindowChecks;

            if (totalChecksForLastTwoWindows == 0)
            {
                // no checks, effectively 100%. don't divide by zero
                return 1;
            }

            // Current window + previous window to prevent burst-iness and low new window numbers from skewing the rate
            return (_windowAllowed + _previousWindowAllowed) / (float)totalChecksForLastTwoWindows;
        }

        private void WaitForRefresh()
        {
            var now = DateTime.Now;
            var previousRefresh = _lastRefresh;

            // Block if a refresh event is happening
            _refreshEvent.Wait();

            if (previousRefresh != _lastRefresh)
            {
                // Some other thread already did this very recently
                // Let's save some cycles
                return;
            }

            try
            {
                // Block threads
                _refreshEvent.Reset();

                _lastRefresh = now;

                var timeSinceRefreshAsRatio = (now - previousRefresh).TotalMilliseconds / (float)_intervalMilliseconds;
                var timeSinceWindowStart = (now - _windowBegin).TotalMilliseconds;

                if (timeSinceWindowStart >= _intervalMilliseconds)
                {
                    // statistical window has passed, shift the counts
                    _previousWindowAllowed = _windowAllowed;
                    _previousWindowChecks = _windowChecks;
                    _windowAllowed = 0;
                    _windowChecks = 0;
                    _windowBegin = now;
                }

                if (timeSinceRefreshAsRatio >= 1)
                {
                    if (!_allowedTracesForInterval.IsEmpty)
                    {
                        // Clear all slots
                        _allowedTracesForInterval = new ConcurrentQueue<ulong>();
                    }

                    // The queue is empty, short-circuit
                    return;
                }

                // Clear ratio for the time passed
                var maxItemsToClear = (int)(timeSinceRefreshAsRatio * _maxTracesPerInterval);
                var cleared = 0;

                while (cleared < maxItemsToClear && _allowedTracesForInterval.TryDequeue(out _))
                {
                    cleared++;
                }
            }
            finally
            {
                // Resume threads
                _refreshEvent.Set();
            }
        }
    }
}
