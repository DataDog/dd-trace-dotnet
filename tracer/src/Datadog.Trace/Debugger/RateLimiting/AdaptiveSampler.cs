// <copyright file="AdaptiveSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// An adaptive streaming (non-remembering) sampler.
    ///
    /// The sampler attempts to generate at most N samples per fixed time window in randomized
    /// fashion. For this it divides the timeline into 'sampling windows' of constant duration. Each
    /// sampling window targets a constant number of samples which are scattered randomly (uniform
    /// distribution) throughout the window duration and once the window is over the real stats of
    /// incoming events and the number of gathered samples is used to recompute the target probability to
    /// use in the following window.
    ///
    /// This will guarantee, if the windows are not excessively large, that the sampler will be able
    /// to adjust to the changes in the rate of incoming events.
    ///
    /// However, there might so rapid changes in incoming events rate that we will optimistically use
    /// all allowed samples well before the current window has elapsed or, on the other end of the
    /// spectrum, there will be to few incoming events and the sampler will not be able to generate the
    /// target number of samples.
    ///
    /// To smooth out these hiccups the sampler maintains an under-sampling budget which can be used
    /// to compensate for too rapid changes in the incoming events rate and maintain the target average
    /// number of samples per window.
    ///
    /// Rates below one sample per second are not a whole number of samples per one-second window. For those,
    /// <see cref="SetRate"/> switches the sampler to admit at most one sample per 1/rate interval and stops the
    /// window timer.
    /// </summary>
    internal sealed class AdaptiveSampler : IAdaptiveSampler
    {
        private const long NoIntervalSample = long.MinValue;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AdaptiveSampler>();

        /// <summary>
        /// Exponential Moving Average (EMA) last element weight.
        /// Check out papers about using EMA for streaming data - eg.
        /// https://nestedsoftware.com/2018/04/04/exponential-moving-average-on-streaming-data-4hhl.24876.html
        ///
        /// Corresponds to 'lookback' of N values:
        /// With T being the index of the most recent value the lookback of N values means that for all values with index
        /// T-K, where K > N, the relative weight of that value computed as (1 - alpha)^K is less or equal than the
        /// weight assigned by a plain arithmetic average (= 1/N).
        /// </summary>
        private readonly double _emaAlpha;
        private readonly object _maintenanceLock = new();
        private readonly TimeSpan _windowDuration;
        private int _samplesPerWindow;

        // Set for rates below one per second: at most one sample per _sampleInterval Stopwatch ticks.
        // A bool keeps the window-sampling hot path free of 64-bit volatile reads, which are interlocked on 32-bit.
        private bool _intervalSampling;
        private long _sampleInterval;
        private long _lastIntervalSampleTimestamp = NoIntervalSample;

        private Counts _countsRef;

        private double _probability = 1;

        private long _samplesBudget;

        private double _totalCountRunningAverage;
        private double _avgSamples;

        private int _budgetLookback;
        private double _budgetAlpha;

        private int _countsSlotIndex;
        private Counts[] _countsSlots;

        private Timer? _timer;
        private Action? _rollWindowCallback;
        private int _disposeState;

        internal AdaptiveSampler(
            TimeSpan windowDuration,
            int samplesPerWindow,
            int averageLookback,
            int budgetLookback,
            Action? rollWindowCallback)
        {
            _windowDuration = windowDuration;
            _timer = new Timer(state => RollWindow(), state: null, windowDuration, windowDuration);
            _totalCountRunningAverage = 0;
            _rollWindowCallback = rollWindowCallback;
            _avgSamples = 0;
            _countsSlots = new Counts[2] { new(), new() };
            if (averageLookback < 1)
            {
                averageLookback = 1;
            }

            if (budgetLookback < 1)
            {
                budgetLookback = 1;
            }

            _budgetLookback = budgetLookback;
            UpdateSamplesPerWindow(samplesPerWindow);

            _emaAlpha = ComputeIntervalAlpha(averageLookback);
            _budgetAlpha = ComputeIntervalAlpha(budgetLookback);

            _countsRef = _countsSlots[0];

            if (windowDuration != TimeSpan.Zero)
            {
                _timer.Change(windowDuration, windowDuration);
            }
        }

        public bool Sample()
        {
            if (Volatile.Read(ref _intervalSampling))
            {
                return SampleOncePerInterval(Stopwatch.GetTimestamp(), Volatile.Read(ref _sampleInterval));
            }

            _countsRef.AddTest();

            if (NextDouble() < _probability)
            {
                return _countsRef.AddSample(Volatile.Read(ref _samplesBudget));
            }

            return false;
        }

        public bool Keep()
        {
            _countsRef.AddTest();
            _countsRef.AddSample();
            return true;
        }

        public bool Drop()
        {
            _countsRef.AddTest();
            return false;
        }

        public double NextDouble()
        {
            return ThreadSafeRandom.Shared.NextDouble();
        }

        /// <summary>
        /// Updates the rate in place. Rates between 0 and 1 admit at most one sample every
        /// 1 / <paramref name="snapshotsPerSecond"/> seconds. Other rates set the samples per window, truncated to a whole number.
        /// </summary>
        public void SetRate(double snapshotsPerSecond)
        {
            lock (_maintenanceLock)
            {
                // Callers already inside Sample() may still finish on the previous mode.
                var wasIntervalSampling = _intervalSampling;
                if (snapshotsPerSecond is > 0 and < 1)
                {
                    Volatile.Write(ref _sampleInterval, GetSampleInterval(snapshotsPerSecond));
                    if (!wasIntervalSampling)
                    {
                        Volatile.Write(ref _intervalSampling, true);
                        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    }

                    return;
                }

                SetSamplesPerWindow((int)snapshotsPerSecond);
                if (wasIntervalSampling)
                {
                    Volatile.Write(ref _intervalSampling, false);
                    _timer?.Change(_windowDuration, _windowDuration);
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            {
                return;
            }

            // SetRate changes the timer under the lock, so detach it there to never change a disposed timer.
            Timer? timer;
            lock (_maintenanceLock)
            {
                timer = _timer;
                _timer = null;
            }

            timer?.Dispose();
            _rollWindowCallback = null;
        }

        internal bool SampleOncePerInterval(long timestamp) => SampleOncePerInterval(timestamp, Volatile.Read(ref _sampleInterval));

        private static long GetSampleInterval(double snapshotsPerSecond)
        {
            var ticks = Math.Round(Stopwatch.Frequency / snapshotsPerSecond);
            return ticks >= long.MaxValue ? long.MaxValue : (long)ticks;
        }

        private bool SampleOncePerInterval(long timestamp, long sampleInterval)
        {
            var lastSample = Volatile.Read(ref _lastIntervalSampleTimestamp);
            if (lastSample != NoIntervalSample && timestamp - lastSample < sampleInterval)
            {
                return false;
            }

            // Only one of the callers racing past the interval wins the slot.
            return Interlocked.CompareExchange(ref _lastIntervalSampleTimestamp, timestamp, lastSample) == lastSample;
        }

        private double ComputeIntervalAlpha(int lookback)
        {
            return 1 - Math.Pow(lookback, -1.0 / lookback);
        }

        private void UpdateSamplesPerWindow(int samplesPerWindow)
        {
            lock (_maintenanceLock)
            {
                SetSamplesPerWindow(samplesPerWindow);
            }
        }

        private void SetSamplesPerWindow(int samplesPerWindow)
        {
            _samplesPerWindow = samplesPerWindow;
            Volatile.Write(ref _samplesBudget, samplesPerWindow + ((long)_budgetLookback * samplesPerWindow));
        }

        private long CalculateBudgetEma(long sampledCount)
        {
            _avgSamples = double.IsNaN(_avgSamples) || _budgetAlpha <= 0.0
                              ? sampledCount
                              : _avgSamples + (_budgetAlpha * (sampledCount - _avgSamples));

            double result = Math.Round(Math.Max(_samplesPerWindow - _avgSamples, 0.0) * _budgetLookback);
            return (long)result;
        }

        internal void RollWindow()
        {
            try
            {
                if (Volatile.Read(ref _disposeState) != 0)
                {
                    return;
                }

                Action? rollWindowCallback;

                lock (_maintenanceLock)
                {
                    var counts = _countsSlots[_countsSlotIndex];

                    // Semi-atomically replace the Counts instance such that sample requests during window maintenance will be
                    // using the newly created counts instead of the ones currently processed by the maintenance routine.
                    // We are ok with slightly racy outcome where totaCount and sampledCount may not be totally in sync
                    // because it allows to avoid contention in the hot-path and the effect on the overall sample rate is minimal
                    // and will get compensated in the long run.
                    // Theoretically, a compensating system might be devised but it will always require introducing a single point
                    // of contention and add a fair amount of complexity. Considering that we are ok with keeping the target sampling
                    // rate within certain error margins and this data race is not breaking the margin it is better to keep the
                    // code simple and reasonably fast.
                    _countsSlotIndex = (_countsSlotIndex + 1) % 2;
                    _countsRef = _countsSlots[_countsSlotIndex];
                    var totalCount = counts.TestCount();
                    var sampledCount = counts.SampleCount();

                    var samplesBudget = CalculateBudgetEma(sampledCount);
                    Volatile.Write(ref _samplesBudget, samplesBudget);

                    if (_totalCountRunningAverage == 0 || _emaAlpha <= 0.0)
                    {
                        _totalCountRunningAverage = totalCount;
                    }
                    else
                    {
                        _totalCountRunningAverage = _totalCountRunningAverage + (_emaAlpha * (totalCount - _totalCountRunningAverage));
                    }

                    if (_totalCountRunningAverage <= 0)
                    {
                        _probability = 1;
                    }
                    else
                    {
                        _probability = Math.Min(samplesBudget / _totalCountRunningAverage, 1.0);
                    }

                    counts.Reset();
                    rollWindowCallback = _rollWindowCallback;
                }

                if (rollWindowCallback != null)
                {
                    rollWindowCallback();
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "AdaptiveSampler - Failed to roll window");
            }
        }

        internal State GetInternalState()
        {
            var counts = _countsRef;

            return new State
            {
                TestCount = counts.TestCount(),
                SampleCount = counts.SampleCount(),
                Budget = Volatile.Read(ref _samplesBudget),
                Probability = _probability,
                TotalAverage = _totalCountRunningAverage,
                SampleInterval = Volatile.Read(ref _intervalSampling) ? Volatile.Read(ref _sampleInterval) : 0
            };
        }

        private sealed class Counts
        {
            private long _testCount;
            private long _sampleCount;

            internal void AddTest()
            {
                Interlocked.Increment(ref _testCount);
            }

            internal bool AddSample(long limit)
            {
                return AtomicInt64.GetAndAccumulate(ref _sampleCount, limit, (prev, lim) => Math.Min(prev + 1, lim)) < limit;
            }

            internal void AddSample()
            {
                Interlocked.Increment(ref _sampleCount);
            }

            internal void Reset()
            {
                _testCount = 0;
                _sampleCount = 0;
            }

            internal long SampleCount()
            {
                return _sampleCount;
            }

            internal long TestCount()
            {
                return _testCount;
            }
        }

        internal sealed class State
        {
            public long TestCount { get; set; }

            public long SampleCount { get; set; }

            public long Budget { get; set; }

            public double Probability { get; set; }

            public double TotalAverage { get; set; }

            public long SampleInterval { get; set; }
        }
    }
}
