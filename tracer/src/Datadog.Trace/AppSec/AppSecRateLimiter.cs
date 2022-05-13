// <copyright file="AppSecRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.AppSec
{
    internal class AppSecRateLimiter : RateLimiter
    {
        public AppSecRateLimiter(int? maxTracesPerInterval)
            : base(maxTracesPerInterval)
        {
        }

        public override void OnDisallowed(Span span, int count, int intervalMs, int maxTracesPerInterval)
        {
            span.SetMetric(Metrics.AppSecRateLimitDroppedTraces, count - maxTracesPerInterval);
        }

        public override void OnFinally(Span span)
        {
        }
    }
}
