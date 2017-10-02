using Moq;
using System;
using Xunit;

namespace Datadog.Tracer.Tests
{
    public class SpanTests
    {
        private Mock<IDatadogTracer> _tracerMock;
        public SpanTests()
        {
            _tracerMock = new Mock<IDatadogTracer>(MockBehavior.Strict);
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

            Assert.Equal("Op1", span.OperationName);
        }

        [Fact]
        public void Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed()
        {
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1);
            var span = new Span(_tracerMock.Object, null, null, startTime);
            _tracerMock
               .Setup(x => x.Write(It.Is<Span>(s => s == span)));

            span.Finish();

            Assert.True(span.Duration > TimeSpan.FromMinutes(1) && span.Duration < TimeSpan.FromMinutes(2));
            _tracerMock
                .Verify(x => x.Write(It.Is<Span>(s => s == span)), Times.Once);
        }

        [Fact]
        public void Finish_NoEndTimeProvided_SpanWriten()
        {
            var span = new Span(_tracerMock.Object, null, null, null);
            _tracerMock
               .Setup(x => x.Write(It.Is<Span>(s => s == span)));

            span.Finish();

            Assert.True(span.Duration > TimeSpan.Zero);
            _tracerMock
                .Verify(x => x.Write(It.Is<Span>(s => s == span)), Times.Once);
        }

        [Fact]
        public void Finish_EndTimeProvided_SpanWritenWithCorrectDuration()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(10);
            var span = new Span(_tracerMock.Object, null, null, startTime);
            _tracerMock
               .Setup(x => x.Write(It.Is<Span>(s => s == span)));

            span.Finish(endTime);

            Assert.Equal(endTime - startTime, span.Duration);
            _tracerMock
                .Verify(x => x.Write(It.Is<Span>(s => s == span)), Times.Once);
        }

        [Fact]
        public void Finish_EndTimeInThePast_DurationIs0()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(-10);
            var span = new Span(_tracerMock.Object, null, null, startTime);
            _tracerMock
               .Setup(x => x.Write(It.Is<Span>(s => s == span)));

            span.Finish(endTime);

            Assert.Equal(TimeSpan.Zero, span.Duration);
            _tracerMock
                .Verify(x => x.Write(It.Is<Span>(s => s == span)), Times.Once);
        }

        [Fact]
        public void Dispose_ExitUsing_SpanWriten()
        {
            Span span;
            using (span = new Span(_tracerMock.Object, null, null, null))
            {
                _tracerMock
                   .Setup(x => x.Write(It.Is<Span>(s => s == span)));
            }

            Assert.True(span.Duration > TimeSpan.Zero);
            _tracerMock
                .Verify(x => x.Write(It.Is<Span>(s => s == span)), Times.Once);
        }
    }
}
