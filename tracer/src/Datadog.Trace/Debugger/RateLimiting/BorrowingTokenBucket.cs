// <copyright file="BorrowingTokenBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// A token bucket that refills at the rate and holds one second of tokens, at least one. <see cref="Sample"/> takes a
    /// token if one is left. <see cref="Keep"/> takes one even if none is, so the bucket can go into debt, and
    /// <see cref="Sample"/> refuses until the refill pays it back. It stores only the time the next token is due, so it
    /// is lock-free and allocates nothing.
    /// </summary>
    internal sealed class BorrowingTokenBucket : IAdaptiveSampler
    {
        private readonly long _tokenIntervalTicks;

        // How far before now the next token can be due: the rest of a full bucket.
        private readonly long _burstTicks;

        private long _nextTokenDue;

        [TestingAndPrivateOnly]
        internal BorrowingTokenBucket(double tokensPerSecond)
        {
            if (double.IsNaN(tokensPerSecond) || tokensPerSecond <= 0)
            {
                // No token is ever due, so nothing is admitted.
                _tokenIntervalTicks = long.MaxValue;
                _nextTokenDue = long.MaxValue;
                return;
            }

            // Faster than one token per tick is unlimited in practice, so cap it to keep the interval at least one tick.
            var rate = Math.Min(tokensPerSecond, Stopwatch.Frequency);
            _tokenIntervalTicks = AdaptiveSampler.GetSampleInterval(rate);

            // From the rounded interval, so a full bucket holds exactly its capacity.
            _burstTicks = (long)((Math.Max(rate, 1) - 1) * _tokenIntervalTicks);
            _nextTokenDue = long.MinValue;
        }

        public static IAdaptiveSampler Create(double tokensPerSecond) => new BorrowingTokenBucket(tokensPerSecond);

        public bool Sample() => TryTake(Stopwatch.GetTimestamp());

        public bool Keep()
        {
            Take(Stopwatch.GetTimestamp());
            return true;
        }

        public bool Drop() => false;

        public double NextDouble() => ThreadSafeRandom.Shared.NextDouble();

        public void Dispose()
        {
        }

        [TestingAndPrivateOnly]
        internal bool TryTake(long timestamp)
        {
            while (true)
            {
                var nextTokenDue = Volatile.Read(ref _nextTokenDue);
                if (timestamp < nextTokenDue)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref _nextTokenDue, After(nextTokenDue, timestamp), nextTokenDue) == nextTokenDue)
                {
                    return true;
                }
            }
        }

        [TestingAndPrivateOnly]
        internal void Take(long timestamp)
        {
            while (true)
            {
                var nextTokenDue = Volatile.Read(ref _nextTokenDue);
                if (Interlocked.CompareExchange(ref _nextTokenDue, After(nextTokenDue, timestamp), nextTokenDue) == nextTokenDue)
                {
                    return;
                }
            }
        }

        // The due time after one more token is taken. A bucket idle for longer than it takes to fill holds no more than a
        // full bucket, so the due time stays within the burst of now. It saturates rather than wrapping.
        private long After(long nextTokenDue, long timestamp)
        {
            var from = Math.Max(nextTokenDue, timestamp - _burstTicks);
            return from > long.MaxValue - _tokenIntervalTicks ? long.MaxValue : from + _tokenIntervalTicks;
        }
    }
}
