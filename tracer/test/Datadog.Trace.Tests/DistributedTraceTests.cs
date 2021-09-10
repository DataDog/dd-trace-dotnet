// <copyright file="DistributedTraceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tests
{
    public class DistributedTraceTests
    {
        [Fact]
        public void ManuallyDistributedTrace_CarriesExpectedValues()
        {
            var tracer = new Tracer();

            ulong traceId;
            ulong parentSpanId;
            string samplingPriorityText = null;
            var expectedSamplingPriority = (int)SamplingPriority.UserKeep;

            using (var scope = tracer.StartActive("manual.trace"))
            {
                scope.Span.SetTag(Tags.SamplingPriority, expectedSamplingPriority.ToString());
                traceId = scope.Span.TraceId;

                using (var parentSpanOfDistributedTrace = tracer.StartActive("SortOrders"))
                {
                    parentSpanId = scope.Span.SpanId;
                    samplingPriorityText = parentSpanOfDistributedTrace.Span.GetTag(Tags.SamplingPriority);
                }
            }

            var distributedTraceContext = new SpanContext(traceId, parentSpanId);
            var secondTracer = new Tracer();

            using (var scope = secondTracer.StartActive("manual.trace", parent: distributedTraceContext))
            {
                scope.Span.SetTag(Tags.SamplingPriority, samplingPriorityText);
                Assert.True(scope.Span.TraceId == traceId, "Trace ID must match the parent trace.");
                var actualSamplingPriorityText = scope.Span.GetTag(Tags.SamplingPriority);
                Assert.True(actualSamplingPriorityText.Equals(expectedSamplingPriority.ToString()), "Sampling priority of manual distributed trace must match the original trace.");
            }
        }
    }
}
