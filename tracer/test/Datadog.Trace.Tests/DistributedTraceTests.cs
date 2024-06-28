// <copyright file="DistributedTraceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class DistributedTraceTests
    {
        [Fact]
        public async Task ManuallyDistributedTrace_CarriesExpectedValues()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();

            ulong traceId;
            ulong parentSpanId;
            string samplingPriorityText;
            var expectedSamplingPriority = SamplingPriorityValues.UserKeep;

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
            await using var secondTracer = TracerHelper.CreateWithFakeAgent();
            var spanCreationSettings = new SpanCreationSettings() { Parent = distributedTraceContext };

            using (var scope = secondTracer.StartActive("manual.trace", spanCreationSettings))
            {
                scope.Span.SetTag(Tags.SamplingPriority, samplingPriorityText);
                Assert.True(scope.Span.TraceId == traceId, "Trace ID must match the parent trace.");
                var actualSamplingPriorityText = scope.Span.GetTag(Tags.SamplingPriority);
                Assert.True(actualSamplingPriorityText.Equals(expectedSamplingPriority.ToString()), "Sampling priority of manual distributed trace must match the original trace.");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ManuallyDistributedTrace_RemoteSpanContext_CarriesExpectedValues(bool shouldIgnoreParentSampling)
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();

            ulong traceId;
            var expectedSamplingPriorityText = SamplingPriorityValues.UserKeep.ToString();

            var spanContextPropagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[]
                {
                    ContextPropagationHeaderStyle.Datadog,
                    ContextPropagationHeaderStyle.W3CTraceContext,
                },
                new[]
                {
                    ContextPropagationHeaderStyle.Datadog,
                    ContextPropagationHeaderStyle.W3CTraceContext,
                },
                false,
                shouldIgnoreParentSampling);

            NameValueHeadersCollection headers = new(new NameValueCollection());

            using (var scope = tracer.StartActive("manual.trace"))
            {
                scope.Span.SetTag(Tags.SamplingPriority, expectedSamplingPriorityText);
                traceId = scope.Span.TraceId;

                using (var parentSpanOfDistributedTrace = tracer.StartActive("SortOrders"))
                {
                    spanContextPropagator.Inject((SpanContext)parentSpanOfDistributedTrace.Span.Context, headers);
                }
            }

            var distributedTraceContext = spanContextPropagator.Extract(headers);

            var settings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.PropagationShouldIgnoreParentSampling, shouldIgnoreParentSampling.ToString() },
                { ConfigurationKeys.GlobalSamplingRate, "0" },
            }));

            await using var secondTracer = TracerHelper.CreateWithFakeAgent(settings);
            var spanCreationSettings = new SpanCreationSettings() { Parent = distributedTraceContext };

            using (var scope = secondTracer.StartActive("manual.trace", spanCreationSettings))
            {
                scope.Span.TraceId.Should().Be(traceId);
                var actualSamplingPriorityText = scope.Span.GetTag(Tags.SamplingPriority);

                if (shouldIgnoreParentSampling)
                {
                    actualSamplingPriorityText.Should().BeNull();
                }
                else
                {
                    actualSamplingPriorityText.Should().Be(expectedSamplingPriorityText);
                }
            }
        }
    }
}
