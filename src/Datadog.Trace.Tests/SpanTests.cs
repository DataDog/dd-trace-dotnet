using System;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class SpanTests
    {
        private Mock<IDatadogTracer> _tracerMock;
        private TraceContext _traceContext;

        public SpanTests()
        {
            _tracerMock = new Mock<IDatadogTracer>(MockBehavior.Strict);
            _traceContext = new TraceContext(_tracerMock.Object);
            _tracerMock.Setup(x => x.DefaultServiceName).Returns("DefaultServiceName");
            _tracerMock.Setup(x => x.IsDebugEnabled).Returns(true);
        }

        [Fact]
        public void SetTag_Tags_TagsAreProperlySet()
        {
            var span = new Span(_tracerMock.Object, null, null, null, null);

            span.SetTag("StringKey", "What's tracing");
            span.SetTag("IntKey", 42);
            span.SetTag("DoubleKey", 1.618);
            span.SetTag("BoolKey", true);

            // _tracerMock.Verify(x => x.CloseCurrentTraceContext(), Times.Never);
            Assert.Equal("What's tracing", span.DDSpan.GetTag("StringKey"));
            Assert.Equal("42", span.DDSpan.GetTag("IntKey"));
            Assert.Equal("1.618", span.DDSpan.GetTag("DoubleKey"));
            Assert.Equal("True", span.DDSpan.GetTag("BoolKey"));
        }

        [Fact]
        public void SetOperationName_ValidOperationName_OperationNameIsProperlySet()
        {
            var span = new Span(_tracerMock.Object, null, null, null, null);

            span.SetOperationName("Op1");

            // _tracerMock.Verify(x => x.CloseCurrentTraceContext(), Times.Never);
            Assert.Equal("Op1", span.DDSpan.OperationName);
        }

        [Fact]
        public void Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed()
        {
            // The 10 additional milliseconds account for the clock precision
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1).AddMilliseconds(-10);
            var span = new Span(_tracerMock.Object, null, null, null, startTime);

            span.Finish();

            // _tracerMock.Verify(x => x.CloseCurrentTraceContext(), Times.Once);
            Assert.True(span.DDSpan.Duration >= TimeSpan.FromMinutes(1) && span.DDSpan.Duration < TimeSpan.FromMinutes(2));
        }

        [Fact]
        public async Task Finish_NoEndTimeProvided_SpanWriten()
        {
            var span = new Span(_tracerMock.Object, null, null, null, null);
            await Task.Delay(TimeSpan.FromMilliseconds(1));
            span.Finish();

            // _tracerMock.Verify(x => x.CloseCurrentTraceContext(), Times.Once);
            Assert.True(span.DDSpan.Duration > TimeSpan.Zero);
    }

        [Fact]
        public void Finish_EndTimeProvided_SpanWritenWithCorrectDuration()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(10);
            var span = new Span(_tracerMock.Object, null, null, null, startTime);

            span.Finish(endTime);

            // _tracerMock.Verify(x => x.CloseCurrentTraceContext(), Times.Once);
            Assert.Equal(endTime - startTime, span.DDSpan.Duration);
        }

        [Fact]
        public void Finish_EndTimeInThePast_DurationIs0()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(-10);
            var span = new Span(_tracerMock.Object, null, null, null, startTime);

            span.Finish(endTime);

            // _tracerMock.Verify(x => x.CloseCurrentTraceContext(), Times.Once);
            Assert.Equal(TimeSpan.Zero, span.DDSpan.Duration);
        }

        [Fact]
        public void Dispose_ExitUsing_SpanWriten()
        {
            Span span;
            using (span = new Span(_tracerMock.Object, null, null, null, null))
            {
            }

            // _tracerMock.Verify(x => x.CloseCurrentTraceContext(), Times.Once);
            Assert.True(span.DDSpan.Duration > TimeSpan.Zero);
        }
    }
}
