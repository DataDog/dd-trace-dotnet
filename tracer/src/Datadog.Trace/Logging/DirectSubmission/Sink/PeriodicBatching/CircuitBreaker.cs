// <copyright file="CircuitBreaker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching
{
    /// <summary>
    /// A simple circuit breaker for periodic batching, which, if never succeeds remains permanently broken
    /// </summary>
    internal class CircuitBreaker
    {
        private readonly int _failuresBeforeBroken;
        private bool _hasEverSucceeded = false;
        private int _consecutiveFailureCount = 0;

        private CircuitStatus _state = CircuitStatus.Closed;

        public CircuitBreaker(int failuresBeforeBroken)
        {
            _failuresBeforeBroken = failuresBeforeBroken;
        }

        public CircuitStatus MarkSuccess()
        {
            _hasEverSucceeded = true;
            _consecutiveFailureCount = 0;

            _state = _state switch
            {
                CircuitStatus.HalfBroken => CircuitStatus.Closed,
                CircuitStatus.Broken => CircuitStatus.HalfBroken,
                _ => _state
            };

            return _state;
        }

        public CircuitStatus MarkFailure()
        {
            _consecutiveFailureCount++;

            _state = _state switch
            {
                CircuitStatus.HalfBroken => CircuitStatus.Broken,
                _ when !_hasEverSucceeded && _consecutiveFailureCount >= _failuresBeforeBroken => CircuitStatus.PermanentlyBroken,
                _ when _consecutiveFailureCount >= _failuresBeforeBroken => CircuitStatus.Broken,
                _ => _state,
            };

            return _state;
        }

        public CircuitStatus MarkSkipped()
        {
            _state = _state switch
            {
                // treat it as a success, so we can start testing logs
                CircuitStatus.Broken => CircuitStatus.HalfBroken,
                // otherwise, leave things as they are
                _ => _state,
            };
            return _state;
        }
    }
}
