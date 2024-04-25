// <copyright file="BasicCircuitBreaker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal enum CircuitBreakerState
    {
        /// <summary>
        /// The circuit breaker is opened, don't do anything!
        /// </summary>
        Opened,

        /// <summary>
        /// The circuit breaker is closed, continue doing your work.
        /// </summary>
        Closed
    }

    internal class BasicCircuitBreaker : IDisposable
    {
        private readonly int _maxTimesToTripInTimeInterval;
        private readonly TimeSpan _resetInterval;
        private readonly Timer _timer;
        private int _failureCount;

        public BasicCircuitBreaker(int maxTimesToTripInTimeInterval, TimeSpan resetInterval)
        {
            _maxTimesToTripInTimeInterval = maxTimesToTripInTimeInterval;
            _resetInterval = resetInterval;
            _timer = new Timer(OnTimerIntervalReached, null, Timeout.Infinite, (int)resetInterval.TotalMilliseconds);
        }

        public CircuitBreakerState Trip()
        {
            var newValue = Interlocked.Increment(ref _failureCount);
            if (newValue == 1)
            {
                _timer.Change(_resetInterval, Timeout.InfiniteTimeSpan);
            }

            return newValue > _maxTimesToTripInTimeInterval ? CircuitBreakerState.Opened : CircuitBreakerState.Closed;
        }

        private void OnTimerIntervalReached(object? state)
        {
            Interlocked.Exchange(ref _failureCount, 0);
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
