using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent
{
    /// <summary>
    /// Used to calculate the Trace Keep Rate, tracking the number of
    /// traces kept and dropped that should have been sent to the backend.
    /// Traces ignored due to sampling are not included in these rates.
    /// </summary>
    internal class MovingAverageKeepRateCalculator : IKeepRateCalculator
    {
        private const int DefaultKeepRate = 10;
        private static readonly TimeSpan DefaultBucketDuration = TimeSpan.FromSeconds(1);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MovingAverageKeepRateCalculator>();

        private readonly int _windowSize;
        private readonly TimeSpan _bucketDuration;
        private readonly uint[] _dropped;
        private readonly uint[] _created;

        private readonly CancellationTokenSource _cts = new();
        private readonly TaskCompletionSource<bool> _processExit = new TaskCompletionSource<bool>();

        private int _index = 0;
        private long _totals = 0;

        private long _latestDrops = 0;
        private long _latestKeeps = 0;

        internal MovingAverageKeepRateCalculator(int size, TimeSpan bucketDuration)
        {
            if (size < 0 || size > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Must be a value between 1 and 100");
            }

            _windowSize = size;
            _bucketDuration = bucketDuration;
            _dropped = new uint[size];
            _created = new uint[size];

            if (bucketDuration != Timeout.InfiniteTimeSpan)
            {
                Task.Run(UpdateBucketTaskLoopAsync)
                    .ContinueWith(t => Log.Error(t.Exception, "Error in "), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public static MovingAverageKeepRateCalculator CreateDefaultKeepRateCalculator()
            => new MovingAverageKeepRateCalculator(DefaultKeepRate, DefaultBucketDuration);

        /// <summary>
        /// Increment the number of kept traces
        /// </summary>
        public void IncrementKeeps(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Interlocked.Add(ref _latestKeeps, count);
        }

        /// <summary>
        /// Increment the number of dropped traces
        /// </summary>
        public void IncrementDrops(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Interlocked.Add(ref _latestDrops, count);
        }

        /// <summary>
        /// Get the current keep rate for traces
        /// </summary>
        public double GetKeepRate()
        {
            var totals = Interlocked.Read(ref _totals);
            UnpackBits(totals, out var totalDropped, out var totalCreated);
            if (totalCreated == 0)
            {
                return 0;
            }

            return 1 - ((double)totalDropped / totalCreated);
        }

        /// <summary>
        /// Stop updating the buckets. The current Keep rate can continue to be read.
        /// </summary>
        public void CancelUpdates()
        {
            _processExit.TrySetResult(true);
        }

        /// <summary>
        /// Update the current rate. Internal for testing only. Should not be called in normal usage.
        /// </summary>
        internal void UpdateBucket()
        {
            var index = _index;
            var previousDropped = _dropped[_index];
            var previousCreated = _created[_index];

            var latestDropped = Math.Min(Interlocked.Exchange(ref _latestDrops, 0), uint.MaxValue);
            var latestCreated = Math.Min(Interlocked.Exchange(ref _latestKeeps, 0) + latestDropped, uint.MaxValue);

            UnpackBits(_totals, out var previousTotalDropped, out var previousTotalCreated);

            var newTotalDropped = (uint)Math.Min(((long)previousTotalDropped) - previousDropped + latestDropped, uint.MaxValue);
            var newTotalCreated = (uint)Math.Min(((long)previousTotalCreated) - previousCreated + latestCreated, uint.MaxValue);

            // Pack the totals into a single value we can read and write atomically
            var packedTotal = PackBits(newTotalDropped, newTotalCreated);
            Interlocked.Exchange(ref _totals, packedTotal);

            _dropped[_index] = (uint)latestDropped;
            _created[_index] = (uint)latestCreated;
            _index = (index + 1) % _windowSize;
        }

        private async Task UpdateBucketTaskLoopAsync()
        {
            while (true)
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                UpdateBucket();

                await Task.WhenAny(
                               Task.Delay(_bucketDuration),
                               _processExit.Task)
                          .ConfigureAwait(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long PackBits(uint hits, uint total)
        {
            long result = hits;
            result = result << 32;
            return result | (long)total;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UnpackBits(long bits, out uint hits, out uint total)
        {
            hits = (uint)(bits >> 32);
            total = (uint)(bits & 0xffffffffL);
        }
    }
}
