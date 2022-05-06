// <copyright file="SpanCounterKeeperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class SpanCounterKeeperTests
    {
        [Fact]
        public void FullRateLimitScenario()
        {
            var tracer = Mock.Of<IDatadogTracer>();

            var limit = 100;
            var rateLimiter = new SpanCounterKeeper(limit, true);

            SingleRateLimitScenario(tracer, limit, rateLimiter);

            rateLimiter.Reset();

            SingleRateLimitScenario(tracer, limit, rateLimiter);
        }

        private static void SingleRateLimitScenario(IDatadogTracer tracer, int limit, SpanCounterKeeper rateLimiter)
        {
            for (var i = 0; i < limit; i++)
            {
                RunSpanTest(tracer, rateLimiter, SamplingPriorityValues.UserKeep);
            }

            RunSpanTest(tracer, rateLimiter, null, 1.0);
        }

        private static void RunSpanTest(IDatadogTracer tracer, SpanCounterKeeper rateLimiter, int? expectedSamplingPirority, double? expectedExceededTraces = null)
        {
            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "test-service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow);

            rateLimiter.CountAndUserKeepSpan(span);

            traceContext.SamplingPriority.Should().Be(expectedSamplingPirority);

            if (expectedExceededTraces != null)
            {
                var exceededTraces = span.GetMetric(Metrics.AppSecRateLimitDroppedTraces);
                exceededTraces.Should().Be(expectedExceededTraces);
            }
        }
    }
}
