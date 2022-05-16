// <copyright file="TracerRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class TracerRateLimiter : RateLimiter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RateLimiter>();

        public TracerRateLimiter(int? maxTracesPerInterval)
            : base(maxTracesPerInterval)
        {
        }

        public override void OnDisallowed(Span span, int count, int intervalMs, int maxTracesPerInterval)
        {
            Log.Warning<ulong, int, int>("Dropping trace id {TraceId} with count of {Count} for last {Interval}ms.", span.TraceId, count, intervalMs);
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
