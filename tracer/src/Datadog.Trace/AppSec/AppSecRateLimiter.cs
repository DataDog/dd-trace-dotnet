// <copyright file="AppSecRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Sampling;

namespace Datadog.Trace.AppSec
{
    internal sealed class AppSecRateLimiter : RateLimiter
    {
        public AppSecRateLimiter(int? maxTracesPerInterval)
            : base(maxTracesPerInterval)
        {
        }

        protected override void OnDisallowed(in SamplingContext context, int count, int intervalMs, int maxTracesPerInterval)
        {
            context.Tags?.SetMetric(Metrics.AppSecRateLimitDroppedTraces, count - maxTracesPerInterval);
        }
    }
}
