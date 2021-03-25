using System;
using Datadog.Trace.Util;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
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
                PartialFlushEnabled = partialFlush,
                PartialFlushMinSpans = 5
            });

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
            tracer.Verify(t => t.Write(It.IsAny<Span[]>()), Times.Never);

            AddAndCloseSpan();

            // Now we have 5 closed spans, partial flush should kick-in if activated
            if (partialFlush)
            {
                tracer.Verify(t => t.Write(It.Is<Span[]>(s => s.Length == 5)), Times.Once);
            }
            else
            {
                tracer.Verify(t => t.Write(It.IsAny<Span[]>()), Times.Never);
            }

            for (int i = 0; i < 5; i++)
            {
                AddAndCloseSpan();
            }

            // We have 5 more closed spans, partial flush should kick-in a second time if activated
            if (partialFlush)
            {
                tracer.Verify(t => t.Write(It.Is<Span[]>(s => s.Length == 5)), Times.Exactly(2));
            }
            else
            {
                tracer.Verify(t => t.Write(It.IsAny<Span[]>()), Times.Never);
            }

            traceContext.CloseSpan(rootSpan);

            // Now the remaining spans are flushed
            if (partialFlush)
            {
                tracer.Verify(t => t.Write(It.Is<Span[]>(s => s.Length == 1)), Times.Once);
            }
            else
            {
                tracer.Verify(t => t.Write(It.Is<Span[]>(s => s.Length == 11)), Times.Once);
            }
        }
    }
}
