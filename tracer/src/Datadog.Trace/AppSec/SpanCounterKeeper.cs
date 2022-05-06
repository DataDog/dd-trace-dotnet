// <copyright file="SpanCounterKeeper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// There is a requirement to limit the number of spans marked with UserKeep to less than
    /// Counts the number of spans, and applies a UserKeep, if the count is less than the given traceRateLimit.
    /// If Reset is called periodically this class can be used as a rate limiter, for the period that between
    /// calls to Reset.
    /// The design is a compromise, it would have somewhat easier to understand to have the task / thread managing
    /// the counter within this class, but my moving out of this class it makes the class easier to unit test.
    /// </summary>
    internal class SpanCounterKeeper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanCounterKeeper>();

        private readonly int _traceRateLimit;
        private readonly bool _keepTraces;

        private long tracesCount;

        public SpanCounterKeeper(int traceRateLimit, bool keepTraces)
        {
            _traceRateLimit = traceRateLimit;
            _keepTraces = keepTraces;

            if (_traceRateLimit <= 0)
            {
                Log.Warning<int>("Rate limit deactivated, traceRateLimit: {traceRateLimit}", traceRateLimit);
            }
        }

        public void Reset()
        {
            Interlocked.Exchange(ref tracesCount, 0);
        }

        public void CountAndUserKeepSpan(Span span)
        {
            if (!_keepTraces)
            {
                span.SetTraceSamplingPriority(SamplingPriorityValues.AutoReject);
                return;
            }

            var exceededTraces = _traceRateLimit > 0 ? Interlocked.Increment(ref tracesCount) - _traceRateLimit : 0;

            if (exceededTraces <= 0)
            {
                span.SetTraceSamplingPriority(SamplingPriorityValues.UserKeep);
            }
            else
            {
                span.SetMetric(Metrics.AppSecRateLimitDroppedTraces, exceededTraces);
            }
        }
    }
}
