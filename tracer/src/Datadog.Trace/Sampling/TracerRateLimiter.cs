// <copyright file="TracerRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal sealed class TracerRateLimiter : RateLimiter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerRateLimiter>();
        private bool _warningWritten;

        public TracerRateLimiter(int? maxTracesPerInterval, int? intervalMilliseconds)
            : base(maxTracesPerInterval, intervalMilliseconds)
        {
        }

        protected override void OnDisallowed(in SamplingContext context, int count, int intervalMs, int maxTracesPerInterval)
        {
            if (!Volatile.Read(ref _warningWritten))
            {
                Log.Warning<string, int, int>("Rate limiter dropped a trace ({TraceId}) after reaching {Count} traces in the last {Interval}ms.", context.Context.RawTraceId, count, intervalMs);
                _warningWritten = true;
            }
        }

        protected override void OnRefresh(int intervalMs, int checksInLastInterval, int allowedInLastInterval)
        {
            if (Volatile.Read(ref _warningWritten))
            {
                Log.Warning<int, int, int>("Trace rate limit interval reset: Allowed {Allowed} traces out of {Checked} in last {Interval}ms.", allowedInLastInterval, checksInLastInterval, intervalMs);
            }

            _warningWritten = false;
        }
    }
}
