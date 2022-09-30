// <copyright file="SpanRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    /// <summary>
    /// Represents a <see cref="RateLimiter" /> specifically for single span ingestion.
    /// See <see cref="TracerRateLimiter" /> for trace-based rate limiting.
    /// </summary>
    internal class SpanRateLimiter : RateLimiter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanRateLimiter>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanRateLimiter"/> class.
        /// </summary>
        /// <param name="maxTracesPerInterval">
        /// The maximum number of <em>spans</em> allowed per interval.
        /// A negative value indicates no limit.
        /// A <see langword="null"/> value will be converted to a default value.
        /// </param>
        public SpanRateLimiter(int? maxTracesPerInterval)
            : base(maxTracesPerInterval)
        {
        }

        public override void OnDisallowed(Span span, int count, int intervalMs, int maxTracesPerInterval)
        {
            // TODO - copied from "TracerRateLimiter" - should this get logged?
            Log.Warning<ulong, int, int>("Dropping span id {SpanId} with count of {Count} for last {Interval}ms.", span.SpanId, count, intervalMs);
        }

        public override void OnFinally(Span span)
        {
        }
    }
}
