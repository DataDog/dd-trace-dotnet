using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class OpenTracingSpanTests
    {
        private Mock<IAgentWriter> _writerMock;
        private Tracer _tracer;

        public OpenTracingSpanTests()
        {
            _writerMock = new Mock<IAgentWriter>();
            _tracer = new Tracer(_writerMock.Object);
        }

        [Fact]
        public void SetTag_Tags_TagsAreProperlySet()
        {
            Scope scope = _tracer.StartActive(null, null, null, null);
            var span = new OpenTracingSpan(scope.Span);

            span.SetTag("StringKey", "What's tracing");
            span.SetTag("IntKey", 42);
            span.SetTag("DoubleKey", 1.618);
            span.SetTag("BoolKey", true);

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<List<Span>>()), Times.Never);
            Assert.Equal("What's tracing", span.DDSpan.GetTag("StringKey"));
            Assert.Equal("42", span.DDSpan.GetTag("IntKey"));
            Assert.Equal("1.618", span.DDSpan.GetTag("DoubleKey"));
            Assert.Equal("True", span.DDSpan.GetTag("BoolKey"));
        }

        [Fact]
        public void SetOperationName_ValidOperationName_OperationNameIsProperlySet()
        {
            Scope scope = _tracer.StartActive(null, null, null, null);
            var span = new OpenTracingSpan(scope.Span);

            span.SetOperationName("Op1");

            Assert.Equal("Op1", span.DDSpan.OperationName);
        }

        [Fact]
        public void Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed()
        {
            // The 10 additional milliseconds account for the clock precision
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1).AddMilliseconds(-10);

            Scope scope = _tracer.StartActive(null, null, null, startTime);
            var span = new OpenTracingSpan(scope.Span);

            span.Finish();

            Assert.True(span.DDSpan.Duration >= TimeSpan.FromMinutes(1) && span.DDSpan.Duration < TimeSpan.FromMinutes(2));
        }

        [Fact]
        public async Task Finish_NoEndTimeProvided_SpanWriten()
        {
            Scope scope = _tracer.StartActive(null, null, null, null);
            var span = new OpenTracingSpan(scope.Span);

            await Task.Delay(TimeSpan.FromMilliseconds(1));
            span.Finish();

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<List<Span>>()), Times.Once);
            Assert.True(span.DDSpan.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void Finish_EndTimeProvided_SpanWritenWithCorrectDuration()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(10);
            Scope scope = _tracer.StartActive(null, null, null, startTime);
            var span = new OpenTracingSpan(scope.Span);

            span.Finish(endTime);

            Assert.Equal(endTime - startTime, span.DDSpan.Duration);
        }

        [Fact]
        public void Finish_EndTimeInThePast_DurationIs0()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(-10);

            Scope scope = _tracer.StartActive(null, null, null, startTime);
            var span = new OpenTracingSpan(scope.Span);

            span.Finish(endTime);

            Assert.Equal(TimeSpan.Zero, span.DDSpan.Duration);
        }

        [Fact]
        public void Dispose_ExitUsing_SpanWriten()
        {
            OpenTracingSpan span;
            Scope scope = _tracer.StartActive(null, null, null, null);

            using (span = new OpenTracingSpan(scope.Span))
            {
            }

            Assert.True(span.DDSpan.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void Context_TwoCalls_ContextStaysEqual()
        {
            Scope scope = _tracer.StartActive(null, null, null, null);
            var span = new OpenTracingSpan(scope.Span);

            Assert.Equal(span.Context, span.Context);
        }
    }
}
