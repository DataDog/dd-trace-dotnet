// <copyright file="TraceContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(AzureAppServicesTestCollection))]
    [AzureAppServicesRestorer]
    public class TraceContextTests
    {
        private readonly Mock<IDatadogTracer> _tracerMock = new Mock<IDatadogTracer>();

        [Fact]
        public void UtcNow_GivesLegitTime()
        {
            var traceContext = new TraceContext(_tracerMock.Object);

            var now = traceContext.UtcNow;
            var expectedNow = DateTimeOffset.UtcNow;

            Assert.True(expectedNow.Subtract(now) < TimeSpan.FromMilliseconds(30));
        }

        [Fact]
        public void UtcNow_IsMonotonic()
        {
            var traceContext = new TraceContext(_tracerMock.Object);

            var t1 = traceContext.UtcNow;
            var t2 = traceContext.UtcNow;

            Assert.True(t2.Subtract(t1) > TimeSpan.Zero);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FlushPartialTraces(bool partialFlush)
        {
            var tracer = new Mock<IDatadogTracer>();

            tracer.Setup(t => t.Settings).Returns(new Trace.Configuration.TracerSettings
            {
                Exporter = new Trace.Configuration.ExporterSettings()
                {
                    PartialFlushEnabled = partialFlush,
                    PartialFlushMinSpans = 5
                }
            }.Build());

            var traceContext = new TraceContext(tracer.Object);

            void AddAndCloseSpan()
            {
                var span = new Span(new SpanContext(42, SpanIdGenerator.ThreadInstance.CreateNew()), DateTimeOffset.UtcNow);

                traceContext.AddSpan(span);
                traceContext.CloseSpan(span);
            }

            var rootSpan = new Span(new SpanContext(42, SpanIdGenerator.ThreadInstance.CreateNew()), DateTimeOffset.UtcNow);

            traceContext.AddSpan(rootSpan);

            for (int i = 0; i < 4; i++)
            {
                AddAndCloseSpan();
            }

            // At this point in time, we have 4 closed spans in the trace
            tracer.Verify(t => t.Write(It.IsAny<ArraySegment<Span>>()), Times.Never);

            AddAndCloseSpan();

            // Now we have 5 closed spans, partial flush should kick-in if activated
            if (partialFlush)
            {
                tracer.Verify(t => t.Write(It.Is<ArraySegment<Span>>(s => s.Count == 5)), Times.Once);
            }
            else
            {
                tracer.Verify(t => t.Write(It.IsAny<ArraySegment<Span>>()), Times.Never);
            }

            for (int i = 0; i < 5; i++)
            {
                AddAndCloseSpan();
            }

            // We have 5 more closed spans, partial flush should kick-in a second time if activated
            if (partialFlush)
            {
                tracer.Verify(t => t.Write(It.Is<ArraySegment<Span>>(s => s.Count == 5)), Times.Exactly(2));
            }
            else
            {
                tracer.Verify(t => t.Write(It.IsAny<ArraySegment<Span>>()), Times.Never);
            }

            traceContext.CloseSpan(rootSpan);

            // Now the remaining spans are flushed
            if (partialFlush)
            {
                tracer.Verify(t => t.Write(It.Is<ArraySegment<Span>>(s => s.Count == 1)), Times.Once);
            }
            else
            {
                tracer.Verify(t => t.Write(It.Is<ArraySegment<Span>>(s => s.Count == 11)), Times.Once);
            }
        }

        [Fact]
        public void FullFlushShouldNotPropagateSamplingPriority()
        {
            const int partialFlushThreshold = 3;

            Span CreateSpan() => new Span(new SpanContext(42, SpanIdGenerator.ThreadInstance.CreateNew()), DateTimeOffset.UtcNow);

            var tracer = new Mock<IDatadogTracer>();

            tracer.Setup(t => t.Settings).Returns(new Trace.Configuration.TracerSettings
            {
                Exporter = new Trace.Configuration.ExporterSettings()
                {
                    PartialFlushEnabled = true,
                    PartialFlushMinSpans = partialFlushThreshold
                }
            }.Build());

            ArraySegment<Span>? spans = null;

            tracer.Setup(t => t.Write(It.IsAny<ArraySegment<Span>>()))
                  .Callback<ArraySegment<Span>>(s => spans = s);

            var traceContext = new TraceContext(tracer.Object);
            traceContext.SetSamplingDecision(SamplingPriorityValues.UserKeep);

            var rootSpan = CreateSpan();

            traceContext.AddSpan(rootSpan);

            for (int i = 0; i < partialFlushThreshold - 1; i++)
            {
                var span = CreateSpan();
                traceContext.AddSpan(span);
                traceContext.CloseSpan(span);
            }

            // At this point, only one span is missing to reach the threshold for partial flush
            spans.Should().BeNull("partial flush should not have been triggered");

            // Closing the root span brings the number of closed spans to the threshold
            // but a full flush should be triggered rather than a partial, because every span in the trace has been closed
            traceContext.CloseSpan(rootSpan);

            spans.Value.Should().NotBeNullOrEmpty("a full flush should have been triggered");

            rootSpan.GetMetric(Metrics.SamplingPriority).Should().Be(SamplingPriorityValues.UserKeep, "priority should be assigned to the root span");

            spans.Value.Should().OnlyContain(s => s == rootSpan || s.GetMetric(Metrics.SamplingPriority) == null, "only the root span should have a priority");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PartialFlushShouldPropagateMetadata(bool inAASContext)
        {
            const int partialFlushThreshold = 2;

            Span CreateSpan() => new Span(new SpanContext(42, SpanIdGenerator.ThreadInstance.CreateNew()), DateTimeOffset.UtcNow);

            var tracer = new Mock<IDatadogTracer>();

            tracer.Setup(t => t.Settings).Returns(new Trace.Configuration.TracerSettings
            {
                Exporter = new Trace.Configuration.ExporterSettings()
                {
                    PartialFlushEnabled = true,
                    PartialFlushMinSpans = partialFlushThreshold
                }
            }.Build());

            ArraySegment<Span>? spans = null;

            tracer.Setup(t => t.Write(It.IsAny<ArraySegment<Span>>()))
                  .Callback<ArraySegment<Span>>(s => spans = s);

            var vars = Environment.GetEnvironmentVariables();

            if (inAASContext)
            {
                vars.Add(AzureAppServices.AzureAppServicesContextKey, "true");
                vars.Add(AzureAppServices.ResourceGroupKey, "ThisIsAResourceGroup");
                vars.Add(Datadog.Trace.Configuration.ConfigurationKeys.ApiKey, "xxx");
                AzureAppServices.Metadata = new AzureAppServices(vars);
            }

            var traceContext = new TraceContext(tracer.Object);
            traceContext.SetSamplingDecision(SamplingPriorityValues.UserKeep);

            var rootSpan = CreateSpan();

            // Root span will stay open for the duration of the test
            traceContext.AddSpan(rootSpan);

            // Add enough child spans to trigger partial flush
            for (int i = 0; i < partialFlushThreshold; i++)
            {
                var span = CreateSpan();
                traceContext.AddSpan(span);
                traceContext.CloseSpan(span);
            }

            spans.Value.Should().NotBeNullOrEmpty("partial flush should have been triggered");

            spans.Value.Should().OnlyContain(s => (int)s.GetMetric(Metrics.SamplingPriority) == SamplingPriorityValues.UserKeep);

            if (inAASContext)
            {
                spans.Value.Array[0].GetTag(Tags.AzureAppServicesResourceGroup).Should().NotBeNull();
            }
            else
            {
                spans.Value.Array[0].GetTag(Tags.AzureAppServicesResourceGroup).Should().BeNull();
            }
        }
    }
}
