// <copyright file="TraceContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Tests.Util;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class TraceContextTests
    {
        private readonly StubDatadogTracer _tracerMock = new();

        [Fact]
        public void UtcNow_GivesLegitTime()
        {
            var traceContext = new TraceContext(_tracerMock);

            var now = traceContext.Clock.UtcNow;
            var expectedNow = DateTimeOffset.UtcNow;

            // We cannot assume that expectedNow > now due to the difference of accuracy of QPC and UtcNow.
            var allowedVariance = EnvironmentTools.IsOsx()
                                        ? TimeSpan.FromMilliseconds(200) // The clock in virtualized osx is terrible
                                        : TimeSpan.FromMilliseconds(30);
            now.Should().BeCloseTo(expectedNow, allowedVariance);
        }

        [Fact]
        public void UtcNow_IsMonotonic()
        {
            var traceContext = new TraceContext(_tracerMock);

            var t1 = traceContext.Clock.UtcNow;
            DateTimeOffset t2;
            do
            {
                t2 = traceContext.Clock.UtcNow;
            }
            while (t1 == t2);

            t2.Should().BeAfter(t1);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FlushPartialTraces(bool partialFlush)
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.PartialFlushEnabled, partialFlush },
                    { ConfigurationKeys.PartialFlushMinSpans, 5 },
                });
            var tracer = new StubDatadogTracer(settings);

            var traceContext = new TraceContext(tracer);

            void AddAndCloseSpan()
            {
                var span = new Span(new SpanContext(42, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);

                traceContext.AddSpan(span);
                traceContext.CloseSpan(span);
            }

            var rootSpan = new Span(new SpanContext(42, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);

            traceContext.AddSpan(rootSpan);

            for (int i = 0; i < 4; i++)
            {
                AddAndCloseSpan();
            }

            // At this point in time, we have 4 closed spans in the trace
            tracer.WrittenChunks.Should().BeEmpty();

            AddAndCloseSpan();

            // Now we have 5 closed spans, partial flush should kick-in if activated
            if (partialFlush)
            {
                tracer.WrittenChunks.Should().ContainSingle().Which.Count.Should().Be(5);
                tracer.WrittenChunks.Clear();
            }
            else
            {
                tracer.WrittenChunks.Should().BeEmpty();
            }

            for (int i = 0; i < 5; i++)
            {
                AddAndCloseSpan();
            }

            // We have 5 more closed spans, partial flush should kick-in a second time if activated
            if (partialFlush)
            {
                tracer.WrittenChunks.Should().ContainSingle().Which.Count.Should().Be(5);
                tracer.WrittenChunks.Clear();
            }
            else
            {
                tracer.WrittenChunks.Should().BeEmpty();
            }

            traceContext.CloseSpan(rootSpan);

            // Now the remaining spans are flushed
            if (partialFlush)
            {
                tracer.WrittenChunks.Should().ContainSingle().Which.Count.Should().Be(1);
            }
            else
            {
                tracer.WrittenChunks.Should().ContainSingle().Which.Count.Should().Be(11);
            }
        }

        [Fact]
        public void FullFlushShouldNotPropagateSamplingPriority()
        {
            const int partialFlushThreshold = 3;

            Span CreateSpan() => new Span(new SpanContext(42, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);

            var tracer = new StubDatadogTracer(TracerSettings.Create(new()
            {
                { ConfigurationKeys.PartialFlushEnabled, true },
                { ConfigurationKeys.PartialFlushMinSpans, partialFlushThreshold },
            }));

            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep);

            var rootSpan = CreateSpan();

            traceContext.AddSpan(rootSpan);

            for (int i = 0; i < partialFlushThreshold - 1; i++)
            {
                var span = CreateSpan();
                traceContext.AddSpan(span);
                traceContext.CloseSpan(span);
            }

            // At this point, only one span is missing to reach the threshold for partial flush
            tracer.WrittenChunks.Should().BeEmpty("partial flush should not have been triggered");

            // Closing the root span brings the number of closed spans to the threshold
            // but a full flush should be triggered rather than a partial, because every span in the trace has been closed
            traceContext.CloseSpan(rootSpan);

            tracer.WrittenChunks.Should().NotBeNullOrEmpty("a full flush should have been triggered");

            rootSpan.GetMetric(Metrics.SamplingPriority).Should().BeNull("because sampling priority is not added until serialization");

            tracer.WrittenChunks.Should().ContainSingle().Which.Should().OnlyContain(s => s.GetMetric(Metrics.SamplingPriority) == null, "because sampling priority is not added until serialization");
        }

        [Fact]
        public void PartialFlushShouldPropagateMetadata()
        {
            const int partialFlushThreshold = 2;

            Span CreateSpan() => new Span(new SpanContext(42, RandomIdGenerator.Shared.NextSpanId()), DateTimeOffset.UtcNow);

            var tracer = new StubDatadogTracer(TracerSettings.Create(new()
            {
                { ConfigurationKeys.PartialFlushEnabled, true },
                { ConfigurationKeys.PartialFlushMinSpans, partialFlushThreshold },
            }));

            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep);

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

            tracer.WrittenChunks.Should().NotBeEmpty("partial flush should have been triggered");
            tracer.WrittenChunks.Should().ContainSingle().Which.Should().OnlyContain(s => s.GetMetric(Metrics.SamplingPriority) == null, "because sampling priority is not added until serialization");
        }

        [Fact]
        public async Task Null_Service_Names_Dont_Throw()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();

            await using var tracer = TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);

            var span = tracer.StartSpan("operation");
            span.ServiceName = null;
            span.Finish(); // should not throw
        }
    }
}
