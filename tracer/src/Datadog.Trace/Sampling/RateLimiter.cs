// <copyright file="RateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Sampling
{
    internal abstract class RateLimiter : IRateLimiter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RateLimiter>();

        private readonly ConcurrentQueue<DateTime> _intervalQueue = new();

        private readonly int _maxTracesPerInterval;
        private readonly int _intervalMilliseconds;
        private readonly TimeSpan _interval;

        private DateTime _windowBegin;

        private int _refreshing;

        private int _windowChecks = 0;
        private int _windowAllowed = 0;

        private int _previousWindowChecks = 0;
        private int _previousWindowAllowed = 0;

        public RateLimiter(int? maxTracesPerInterval)
        {
            _maxTracesPerInterval = maxTracesPerInterval ?? 100;

            _intervalMilliseconds = 1_000;
            _interval = TimeSpan.FromMilliseconds(_intervalMilliseconds);
            _windowBegin = Clock.UtcNow;
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
                    OnDisallowed(span, count, _intervalMilliseconds, _maxTracesPerInterval);

                    return false;
                }

                _intervalQueue.Enqueue(Clock.UtcNow);
                Interlocked.Increment(ref _windowAllowed);

                return true;
            }
            finally
            {
                OnFinally(span);
            }
        }

        public abstract void OnDisallowed(Span span, int count, int intervalMs, int maxTracesPerInterval);

        public abstract void OnFinally(Span span);

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
            int refreshInProgress = 0;

            try
            {
                refreshInProgress = Interlocked.CompareExchange(ref _refreshing, 1, 0);

                if (refreshInProgress != 0)
                {
                    // A refresh is already in progress
                    return;
                }

                var now = Clock.UtcNow;

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
                if (refreshInProgress == 0)
                {
                    // If refreshing is 0, it means that this thread acquired the lock
                    // Releasing it before leaving
                    // Note: a full fence might not be needed here, but better safe than sorry
                    Interlocked.Exchange(ref _refreshing, 0);
                }
            }
        }
    }
}
