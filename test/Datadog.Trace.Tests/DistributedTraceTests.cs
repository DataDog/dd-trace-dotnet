using System;
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
            SamplingPriority? samplingPriority = null;
            var expectedSamplingPriority = (int)SamplingPriority.AutoKeep;

            using (var scope = tracer.StartActive("manual.trace"))
            {
                scope.Span.SetTag(Tags.SamplingPriority, expectedSamplingPriority.ToString());
                traceId = scope.Span.TraceId;

                using (var parentSpanOfDistributedTrace = tracer.StartActive("SortOrders"))
                {
                    parentSpanId = scope.Span.SpanId;
                    var samplingPriorityText = parentSpanOfDistributedTrace.Span.GetTag(Tags.SamplingPriority);
                    if (samplingPriorityText != null && int.TryParse(samplingPriorityText, out var samplingPriorityInt))
                    {
                        samplingPriority = (SamplingPriority)samplingPriorityInt;
                    }
                }
            }

            if (samplingPriority == null)
            {
                throw new Exception("Sampling priority should not be null for this test");
            }

            var distributedTraceContext = new SpanContext(traceId, parentSpanId, samplingPriority);

            using (var scope = Tracer.Instance.StartActive("manual.trace", parent: distributedTraceContext))
            {
                Assert.True(scope.Span.TraceId == traceId, "Trace ID must match the parent trace.");
                var samplingPriorityText = scope.Span.GetTag(Tags.SamplingPriority);
                Assert.True(samplingPriorityText.Equals(expectedSamplingPriority.ToString()), "Sampling priority of manual distributed trace must match the original trace.");
            }
        }
    }
}
