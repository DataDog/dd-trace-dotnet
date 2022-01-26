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

        private int _rateLimiterCounter;
        private int _exceededTraces;

        public RateLimiterTimer(ref int rateLimiterCounter, ref int exceededTraces)
        {
            _rateLimiterCounter = rateLimiterCounter;
            _exceededTraces = exceededTraces;

            _timer = new Timer(
                   _ =>
                   {
                       DoPeriodically();
                   },
                   null,
                   TimeSpan.Zero,
                   TimeSpan.FromSeconds(1));
        }

        public void Dispose() => _timer.Dispose();

        public void DoPeriodically()
        {
            Interlocked.Exchange(ref _rateLimiterCounter, 0);
            Interlocked.Exchange(ref _exceededTraces, 0);
        }
    }
}
