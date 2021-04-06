using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent
{
    /// <summary>
    /// Used to calculate the Trace Keep Rate, tracking the number of
    /// traces kept and dropped that should have been sent to the agent.
    /// Traces that are subsequently dropped by the agent due to sampling
    /// will not count as dropped in this rate.
    /// </summary>
    internal class MovingAverageKeepRateCalculator : IKeepRateCalculator
    {
        private const int DefaultWindowSize = 10;
        private static readonly TimeSpan DefaultBucketDuration = TimeSpan.FromSeconds(1);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MovingAverageKeepRateCalculator>();

        private readonly int _windowSize;
        private readonly TimeSpan _bucketDuration;
        private readonly uint[] _dropped;
        private readonly uint[] _created;

        private readonly TaskCompletionSource<bool> _processExit = new TaskCompletionSource<bool>();

        private int _index = 0;
        private long _totals = 0;

        private long _latestDrops = 0;
        private long _latestKeeps = 0;

        internal MovingAverageKeepRateCalculator(int windowSize, TimeSpan bucketDuration)
        {
            if (windowSize < 0 || windowSize > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize), windowSize, "Must be a value between 1 and 100");
            }

            _windowSize = windowSize;
            _bucketDuration = bucketDuration;
            _dropped = new uint[windowSize];
            _created = new uint[windowSize];

            if (bucketDuration != Timeout.InfiniteTimeSpan)
            {
                Task.Run(UpdateBucketTaskLoopAsync)
                    .ContinueWith(t => Log.Error(t.Exception, $"Error in {nameof(MovingAverageKeepRateCalculator)} {nameof(UpdateBucketTaskLoopAsync)} "), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public static MovingAverageKeepRateCalculator CreateDefaultKeepRateCalculator()
            => new MovingAverageKeepRateCalculator(DefaultWindowSize, DefaultBucketDuration);

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
            var previousDropped = _dropped[index];
            var previousCreated = _created[index];

            var latestDropped = Math.Min(Interlocked.Exchange(ref _latestDrops, 0), uint.MaxValue);
            var latestCreated = Math.Min(Interlocked.Exchange(ref _latestKeeps, 0) + latestDropped, uint.MaxValue);

            var totals = _totals;
            UnpackBits(totals, out var previousTotalDropped, out var previousTotalCreated);

            var newTotalDropped = (uint)Math.Min(((long)previousTotalDropped) - previousDropped + latestDropped, uint.MaxValue);
            var newTotalCreated = (uint)Math.Min(((long)previousTotalCreated) - previousCreated + latestCreated, uint.MaxValue);

            // Pack the totals into a single value we can read and write atomically
            var packedTotal = PackBits(newTotalDropped, newTotalCreated);
            Interlocked.Exchange(ref _totals, packedTotal);

            _dropped[index] = (uint)latestDropped;
            _created[index] = (uint)latestCreated;
            _index = (index + 1) % _windowSize;
        }

        private async Task UpdateBucketTaskLoopAsync()
        {
#if !NET5_0_OR_GREATER
            var tasks = new Task[2];
            tasks[0] = _processExit.Task;
#endif
            while (true)
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                UpdateBucket();

#if NET5_0_OR_GREATER
                // .NET 5.0 has an explicit overload for this
                await Task.WhenAny(
                               Task.Delay(_bucketDuration),
                               _processExit.Task)
                          .ConfigureAwait(false);
#else
                tasks[1] = Task.Delay(_bucketDuration);
                await Task.WhenAny(tasks).ConfigureAwait(false);
#endif
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
