using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class RateLimiter : IRateLimiter
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<RateLimiter>();

        private readonly ManualResetEventSlim _refreshEvent = new ManualResetEventSlim(initialState: true);
        private readonly ConcurrentQueue<DateTime> _intervalQueue = new ConcurrentQueue<DateTime>();

        private readonly int _maxTracesPerInterval;
        private readonly int _intervalMilliseconds;
        private readonly TimeSpan _interval;

        private DateTime _windowBegin;
        private DateTime _lastRefresh;

        private int _windowChecks = 0;
        private int _windowAllowed = 0;

        private int _previousWindowChecks = 0;
        private int _previousWindowAllowed = 0;

        public RateLimiter(int? maxTracesPerInterval)
        {
            _maxTracesPerInterval = maxTracesPerInterval ?? 100;
            _intervalMilliseconds = 1_000;
            _interval = TimeSpan.FromMilliseconds(_intervalMilliseconds);
            _windowBegin = _lastRefresh = DateTime.Now;
        }

        public bool Allowed(Span span)
        {
            try
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

                var count = _intervalQueue.Count;

                if (count >= _maxTracesPerInterval)
                {
                    Log.Debug("Dropping trace id {0} with count of {1} for last {2}ms.", span.TraceId, count, _intervalMilliseconds);
                    return false;
                }

                _intervalQueue.Enqueue(DateTime.Now);
                Interlocked.Increment(ref _windowAllowed);

                return true;
            }
            finally
            {
                // Always set the sample rate metric whether it was allowed or not
                // DEV: Setting this allows us to properly compute metrics and debug the
                //      various sample rates that are getting applied to this span
                span.SetMetric(Metrics.SamplingLimitDecision, GetEffectiveRate());
            }
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
            var previousRefresh = _lastRefresh;

            // Block if a refresh event is happening
            _refreshEvent.Wait();

            if (previousRefresh != _lastRefresh)
            {
                // Some other thread already did this very recently
                // Let's save some cycles and prevent contention
                return;
            }

            try
            {
                // Block threads
                _refreshEvent.Reset();

                var now = DateTime.Now;
                _lastRefresh = now;

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

                while (_intervalQueue.TryPeek(out var time) && now.Subtract(time) > _interval)
                {
                    _intervalQueue.TryDequeue(out _);
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
