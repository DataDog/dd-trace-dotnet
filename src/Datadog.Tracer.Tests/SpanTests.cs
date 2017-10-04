using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.Tracer.Tests
{
    public class SpanTests
    {
        private Mock<IDatadogTracer> _tracerMock;
        private Mock<ITraceContext> _traceContextMock;

        public SpanTests()
        {
            _tracerMock = new Mock<IDatadogTracer>(MockBehavior.Strict);
            _traceContextMock = new Mock<ITraceContext>(MockBehavior.Strict);
            _traceContextMock.Setup(x => x.CloseSpan(It.IsAny<Span>()));
            _traceContextMock.Setup(x => x.GetCurrentSpanContext()).Returns<SpanContext>(null);
            _tracerMock.Setup(x => x.GetTraceContext()).Returns(_traceContextMock.Object);
            _tracerMock.Setup(x => x.DefaultServiceName).Returns("DefaultServiceName");
        }

        [Fact]
        public void SetTag_Tags_TagsAreProperlySet()
        {
            var span = new Span(_tracerMock.Object, null, null, null);

            span.SetTag("StringKey", "What's tracing");
            span.SetTag("IntKey", 42);
            span.SetTag("DoubleKey", 1.618);
            span.SetTag("BoolKey", true);

            _traceContextMock.Verify(x => x.CloseSpan(It.IsAny<Span>()), Times.Never);
            Assert.Equal("What's tracing", span.GetTag("StringKey"));
            Assert.Equal("42", span.GetTag("IntKey"));
            Assert.Equal("1.618", span.GetTag("DoubleKey"));
            Assert.Equal("True", span.GetTag("BoolKey"));
        }

        [Fact]
        public void SetOperationName_ValidOperationName_OperationNameIsProperlySet()
        {
            var span = new Span(_tracerMock.Object, null, null, null);

            span.SetOperationName("Op1");

            _traceContextMock.Verify(x => x.CloseSpan(It.IsAny<Span>()), Times.Never);
            Assert.Equal("Op1", span.OperationName);
        }

        [Fact]
        public void Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed()
        {
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1);
            var span = new Span(_tracerMock.Object, null, null, startTime);

            span.Finish();

            _traceContextMock.Verify(x => x.CloseSpan(It.IsAny<Span>()), Times.Once);
            Assert.True(span.Duration > TimeSpan.FromMinutes(1) && span.Duration < TimeSpan.FromMinutes(2));
        }

        [Fact]
        public async Task Finish_NoEndTimeProvided_SpanWriten()
        {
            var span = new Span(_tracerMock.Object, null, null, null);
            await Task.Delay(TimeSpan.FromMilliseconds(1));
            span.Finish();

            _traceContextMock.Verify(x => x.CloseSpan(It.IsAny<Span>()), Times.Once);
            Assert.True(span.Duration > TimeSpan.Zero);
    }

        [Fact]
        public void Finish_EndTimeProvided_SpanWritenWithCorrectDuration()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(10);
            var span = new Span(_tracerMock.Object, null, null, startTime);

            span.Finish(endTime);

            _traceContextMock.Verify(x => x.CloseSpan(It.IsAny<Span>()), Times.Once);
            Assert.Equal(endTime - startTime, span.Duration);
        }

        [Fact]
        public void Finish_EndTimeInThePast_DurationIs0()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(-10);
            var span = new Span(_tracerMock.Object, null, null, startTime);

            span.Finish(endTime);

            _traceContextMock.Verify(x => x.CloseSpan(It.IsAny<Span>()), Times.Once);
            Assert.Equal(TimeSpan.Zero, span.Duration);
        }

        [Fact]
        public void Dispose_ExitUsing_SpanWriten()
        {
            Span span;
            using (span = new Span(_tracerMock.Object, null, null, null))
            {
            }

            _traceContextMock.Verify(x => x.CloseSpan(It.IsAny<Span>()), Times.Once);
            Assert.True(span.Duration > TimeSpan.Zero);
        }
    }
}
