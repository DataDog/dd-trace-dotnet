// <copyright file="TracerRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class TracerRateLimiter : RateLimiter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerRateLimiter>();
        private bool _warningWritten;

        public TracerRateLimiter(int? maxTracesPerInterval, int? intervalMilliseconds)
            : base(maxTracesPerInterval, intervalMilliseconds)
        {
        }

        public override void OnDisallowed(Span span, int count, int intervalMs, int maxTracesPerInterval)
        {
            if (!Volatile.Read(ref _warningWritten))
            {
                Log.Warning<string, int, int>("Rate limiter dropped a trace ({TraceId}) after reaching {Count} traces in the last {Interval}ms.", span.Context.RawTraceId, count, intervalMs);
                _warningWritten = true;
            }
        }

        public override void OnRefresh(int intervalMs, int checksInLastInterval, int allowedInLastInterval)
        {
            if (Volatile.Read(ref _warningWritten))
            {
                Log.Warning<int, int, int>("Trace rate limit interval reset: Allowed {Allowed} traces out of {Checked} in last {Interval}ms.", allowedInLastInterval, checksInLastInterval, intervalMs);
            }

            _warningWritten = false;
        }

        public override void OnFinally(Span span)
        {
            // Always set the sample rate metric whether it was allowed or not
            // DEV: Setting this allows us to properly compute metrics and debug the
            //      various sample rates that are getting applied to this span
            span.SetMetric(Metrics.SamplingLimitDecision, GetEffectiveRate());
        }
    }
}
