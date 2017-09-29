using Moq;
using System;
using Xunit;

namespace Datadog.Tracer.Tests
{
    public class SpanTests
    {
        [Fact]
        public void SetTag_Tags_TagsAreProperlySet()
        {
            var span = new Span(null, null, null);

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
            var span = new Span(null, null, null);

            span.SetOperationName("Op1");

            Assert.Equal("Op1", span.OperationName);
        }

        [Fact]
        public void Finish_NoEndTimeProvided_SpanWriten()
        {
            var tracerMock = new Mock<IDatadogTracer>(MockBehavior.Strict);
            var span = new Span(tracerMock.Object, null, null);
            tracerMock
               .Setup(x => x.Write(It.Is<Span>(s => s == span)));

            span.Finish();

            Assert.True(DateTimeOffset.UtcNow - span.EndTime < TimeSpan.FromMinutes(1));
            tracerMock
                .Verify(x => x.Write(It.Is<Span>(s => s == span)), Times.Once);
        }

        [Fact]
        public void Finish_EndTimeProvided_SpanWritenWithCorrectEndTime()
        {
            var endTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            var tracerMock = new Mock<IDatadogTracer>(MockBehavior.Strict);
            var span = new Span(tracerMock.Object, null, null);
            tracerMock
               .Setup(x => x.Write(It.Is<Span>(s => s == span)));

            span.Finish(endTime);

            Assert.Equal(endTime, span.EndTime);
            tracerMock
                .Verify(x => x.Write(It.Is<Span>(s => s == span)), Times.Once);
        }

        [Fact]
        public void Dispose_ExitUsing_SpanWriten()
        {
            var endTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            var tracerMock = new Mock<IDatadogTracer>(MockBehavior.Strict);
            Span span;
            using (span = new Span(tracerMock.Object, null, null))
            {
                tracerMock
                   .Setup(x => x.Write(It.Is<Span>(s => s == span)));
            }

            Assert.True(DateTimeOffset.UtcNow - span.EndTime < TimeSpan.FromMinutes(1));
            tracerMock
                .Verify(x => x.Write(It.Is<Span>(s => s == span)), Times.Once);
        }
    }
}
