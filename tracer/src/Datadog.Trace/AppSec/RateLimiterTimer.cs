// <copyright file="RateLimiterTimer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

namespace Datadog.Trace.AppSec
{
    internal class RateLimiterTimer : IDisposable
    {
        private readonly Timer _timer;
        private readonly int _traceRateLimit;
        private int _rateLimiterCounter;
        private int _exceededTraces;

        public RateLimiterTimer(int traceRateLimit)
        {
            _traceRateLimit = traceRateLimit;
            _timer = new Timer(
                   _ =>
                   {
                       Reset();
                   },
                   null,
                   TimeSpan.Zero,
                   TimeSpan.FromSeconds(1));
        }

        public int RateLimiterCounter => _rateLimiterCounter;

        public int ExceededTraces => _exceededTraces;

        public void Dispose() => _timer.Dispose();

        public void Reset()
        {
            Interlocked.Exchange(ref _rateLimiterCounter, 0);
            Interlocked.Exchange(ref _exceededTraces, 0);
        }

        /// <summary>
        /// check if a trace can be added, otherwise increase the amount of exceeded traces
        /// </summary>
        /// <returns>returns the number of exceeded traces, 0 if ok</returns>
        public int UpdateTracesCounter()
        {
            if (_rateLimiterCounter < _traceRateLimit)
            {
                Interlocked.Increment(ref _rateLimiterCounter);
            }
            else
            {
                Interlocked.Increment(ref _exceededTraces);
            }

            return _exceededTraces;
        }

        public void IncrementExceededTraces()
        {
            Interlocked.Increment(ref _exceededTraces);
        }
    }
}
