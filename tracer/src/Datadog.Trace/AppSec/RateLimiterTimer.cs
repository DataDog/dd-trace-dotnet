// <copyright file="RateLimiterTimer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec
{
    internal class RateLimiterTimer : IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RateLimiterTimer>();
        private readonly Timer _timer;
        private readonly int _traceRateLimit;
        private long tracesCount;

        public RateLimiterTimer(int traceRateLimit)
        {
            _traceRateLimit = traceRateLimit;
            if (_traceRateLimit > 0)
            {
                _timer = new Timer(
                       _ =>
                       {
                           Interlocked.Exchange(ref tracesCount, 0);
                       },
                       null,
                       TimeSpan.Zero,
                       TimeSpan.FromSeconds(1));
            }
            else
            {
                Log.Warning<int>("Rate limit deactivated, traceRateLimit: {traceRateLimit}", traceRateLimit);
                _timer = null;
            }
        }

        public long RateLimiterCounter => tracesCount;

        public void Dispose() => _timer?.Dispose();

        /// <summary>
        /// check if a trace can be added, otherwise increase the amount of exceeded traces
        /// </summary>
        /// <returns>returns the number of exceeded traces, 0 if ok</returns>
        public long UpdateTracesCounter()
        {
            return _traceRateLimit > 0 ? Interlocked.Increment(ref tracesCount) - _traceRateLimit : 0;
        }
    }
}
