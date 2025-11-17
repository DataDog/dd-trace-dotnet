// <copyright file="ProtectedSampler.cs" company="Datadog">
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
    /// Composite sampler that orchestrates multiple protection layers:
    /// 1. Kill switch (global emergency stop)
    /// 2. Thread-local prefilter (ultra-cheap rejection)
    /// 3. Global budget check (CPU/time limit)
    /// 3.5. Memory pressure check (memory limit)
    /// 4. Circuit breaker (per-probe protection)
    /// 5. Adaptive sampler (rate limiting)
    /// 6. Degradation level selection (Full/Light/Skip)
    ///
    /// Thread Safety: All methods are thread-safe.
    /// Performance: Hot path (Sample()) is optimized for speed with early-exit checks.
    /// </summary>
    internal class ProtectedSampler : IAdaptiveSampler, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ProtectedSampler>();

        private readonly string _probeId;
        private readonly IAdaptiveSampler _innerSampler;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly IGlobalBudget _globalBudget;
        private readonly IMemoryPressureMonitor? _memoryPressureMonitor;
        private readonly double _memoryDegradeStartPercent;
        private readonly double _memoryDegradeAlwaysLightPercent;
        private volatile bool _killSwitch;
        private int _memoryPressureRecorded; // one-shot marker to avoid repeated Interlocked during sustained pressure

        public ProtectedSampler(
            string probeId,
            IAdaptiveSampler innerSampler,
            ICircuitBreaker circuitBreaker,
            IGlobalBudget globalBudget,
            IMemoryPressureMonitor? memoryPressureMonitor = null,
            double memoryDegradationThresholdPercent = 70.0)
        {
            _probeId = probeId ?? throw new ArgumentNullException(nameof(probeId));
            _innerSampler = innerSampler ?? throw new ArgumentNullException(nameof(innerSampler));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
            _globalBudget = globalBudget ?? throw new ArgumentNullException(nameof(globalBudget));
            _memoryPressureMonitor = memoryPressureMonitor;

            // Configure degradation thresholds:
            // - Start probabilistic degradation at configured threshold (default 70%)
            // - Always use Light capture at 10% above that (capped at 100%)
#if NETCOREAPP3_1_OR_GREATER
            _memoryDegradeStartPercent = Math.Clamp(memoryDegradationThresholdPercent, 0.0, 100.0);
#else
            _memoryDegradeStartPercent = memoryDegradationThresholdPercent < 0.0 ? 0.0 : (memoryDegradationThresholdPercent > 100.0 ? 100.0 : memoryDegradationThresholdPercent);
#endif
            _memoryDegradeAlwaysLightPercent = Math.Min(100.0, _memoryDegradeStartPercent + 10.0);
            _killSwitch = false;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the kill switch is active (emergency stop)
        /// </summary>
        public bool KillSwitch
        {
            get => _killSwitch;
            set => _killSwitch = value;
        }

        /// <summary>
        /// Gets the current circuit breaker state
        /// </summary>
        public CircuitState CircuitState => _circuitBreaker.State;

        /// <summary>
        /// Main sampling decision with all protection layers
        /// </summary>
        public bool Sample()
        {
            // Layer 1: Kill switch (emergency stop)
            if (_killSwitch)
            {
                return false;
            }

            // Layer 2: Thread-local prefilter (ultra-cheap)
            if (!ThreadLocalPrefilter.ShouldAllow())
            {
                return false;
            }

            // Layer 3: Global budget check
            if (_globalBudget.IsExhausted)
            {
                return false;
            }

            // Layer 3.5: Memory pressure check
            if (_memoryPressureMonitor?.IsHighMemoryPressure == true)
            {
                // Record the pressure event once per sustained period to reduce hot-path atomics.
                if (Interlocked.CompareExchange(ref _memoryPressureRecorded, 1, 0) == 0)
                {
                    _circuitBreaker.RecordMemoryPressure();
                }

                return false;
            }
            else
            {
                // Clear the one-shot when pressure subsides.
                Volatile.Write(ref _memoryPressureRecorded, 0);
            }

            // Layer 4: Circuit breaker
            if (!_circuitBreaker.ShouldAllow())
            {
                return false;
            }

            // Layer 5: Adaptive sampler (rate limiting)
            return _innerSampler.Sample();
        }

        /// <summary>
        /// Samples and returns the capture behavior to use
        /// </summary>
        public bool SampleWithBehaviour(out CaptureBehaviour behaviour)
        {
            // Layer 1: Kill switch
            if (_killSwitch)
            {
                behaviour = CaptureBehaviour.Skip;
                return false;
            }

            // Layer 2: Thread-local prefilter
            if (!ThreadLocalPrefilter.ShouldAllow())
            {
                behaviour = CaptureBehaviour.Skip;
                return false;
            }

            // Layer 3: Global budget check
            if (_globalBudget.IsExhausted)
            {
                behaviour = CaptureBehaviour.Skip;
                return false;
            }

            // Layer 3.5: Memory pressure check
            if (_memoryPressureMonitor?.IsHighMemoryPressure == true)
            {
                behaviour = CaptureBehaviour.Skip;
                if (Interlocked.CompareExchange(ref _memoryPressureRecorded, 1, 0) == 0)
                {
                    _circuitBreaker.RecordMemoryPressure();
                }

                return false;
            }
            else
            {
                Volatile.Write(ref _memoryPressureRecorded, 0);
            }

            // Layer 4: Circuit breaker
            if (!_circuitBreaker.ShouldAllow())
            {
                behaviour = CaptureBehaviour.Skip;
                return false;
            }

            // Layer 5: Adaptive sampler
            if (!_innerSampler.Sample())
            {
                behaviour = CaptureBehaviour.Skip;
                return false;
            }

            // Determine capture behaviour based on pressure
            behaviour = DetermineBehaviour();
            return true;
        }

        /// <summary>
        /// Records the execution time and updates all protection layers
        /// </summary>
        public void RecordExecution(long elapsedTicks, bool success)
        {
            // Update global budget
            _globalBudget.RecordUsage(elapsedTicks);

            // Update circuit breaker
            if (success)
            {
                _circuitBreaker.RecordSuccess(elapsedTicks);
            }
            else
            {
                _circuitBreaker.RecordFailure();
            }
        }

        /// <summary>
        /// Marks this probe as being in a hot loop
        /// </summary>
        public void MarkHotLoop()
        {
            _circuitBreaker.RecordHotLoop();
        }

        public bool Keep()
        {
            return _innerSampler.Keep();
        }

        public bool Drop()
        {
            return _innerSampler.Drop();
        }

        public double NextDouble()
        {
            return _innerSampler.NextDouble();
        }

        public void Dispose()
        {
            if (_innerSampler is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (_circuitBreaker is IDisposable circuitDisposable)
            {
                circuitDisposable.Dispose();
            }
        }

        /// <summary>
        /// Determines the capture behaviour based on current pressure.
        /// Uses shared thread-safe RNG to avoid allocations and contention.
        /// Considers both CPU and memory pressure.
        /// </summary>
        private CaptureBehaviour DetermineBehaviour()
        {
            var globalCpuUsage = _globalBudget.GetUsagePercentage();
            var memoryUsagePercent = _memoryPressureMonitor?.MemoryUsagePercent ?? 0;
            var circuitState = _circuitBreaker.State;

            // HIGH PRESSURE: Either resource is critical -> Light capture
            if (circuitState == CircuitState.HalfOpen ||
                globalCpuUsage > 75 ||
                memoryUsagePercent >= _memoryDegradeAlwaysLightPercent)
            {
                return CaptureBehaviour.Light;
            }

            // MODERATE PRESSURE: Either resource moderately high -> probabilistic degradation
            if (globalCpuUsage > 50 || memoryUsagePercent >= _memoryDegradeStartPercent)
            {
                // Higher pressure = more likely to degrade
                var maxPressure = Math.Max(globalCpuUsage / 100.0, memoryUsagePercent / 100.0);
                var degradeProbability = (maxPressure - 0.5) / 0.5; // 0.5->0%, 1.0->100%

                return ThreadSafeRandom.Shared.NextDouble() < degradeProbability
                    ? CaptureBehaviour.Light
                    : CaptureBehaviour.Full;
            }

            // LOW PRESSURE: Both resources healthy -> Full capture
            return CaptureBehaviour.Full;
        }
    }
}
