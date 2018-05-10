using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Moq;
using Xunit;

namespace Datadog.Trace.OpenTracing.Tests
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
            var span = new OpenTracingSpan(_tracer.StartActive(null, null, null, null));

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
            var span = new OpenTracingSpan(_tracer.StartActive(null, null, null, null));

            span.SetOperationName("Op1");

            Assert.Equal("Op1", span.DDSpan.OperationName);
        }

        [Fact]
        public void Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed()
        {
            // The 10 additional milliseconds account for the clock precision
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1).AddMilliseconds(-10);
            var span = new OpenTracingSpan(_tracer.StartActive(null, null, null, startTime));

            span.Finish();

            Assert.True(span.DDSpan.Duration >= TimeSpan.FromMinutes(1) && span.DDSpan.Duration < TimeSpan.FromMinutes(2));
        }

        [Fact]
        public async Task Finish_NoEndTimeProvided_SpanWriten()
        {
            var span = new OpenTracingSpan(_tracer.StartActive(null, null, null, null));
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
            var span = new OpenTracingSpan(_tracer.StartActive(null, null, null, startTime));

            span.Finish(endTime);

            Assert.Equal(endTime - startTime, span.DDSpan.Duration);
        }

        [Fact]
        public void Finish_EndTimeInThePast_DurationIs0()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(-10);
            var span = new OpenTracingSpan(_tracer.StartActive(null, null, null, startTime));

            span.Finish(endTime);

            Assert.Equal(TimeSpan.Zero, span.DDSpan.Duration);
        }

        [Fact]
        public void Dispose_ExitUsing_SpanWriten()
        {
            OpenTracingSpan span;
            using (span = new OpenTracingSpan(_tracer.StartActive(null, null, null, null)))
            {
            }

            Assert.True(span.DDSpan.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void Context_TwoCalls_ContextStaysEqual()
        {
            var span = new OpenTracingSpan(_tracer.StartActive(null, null, null, null));

            Assert.Equal(span.Context, span.Context);
        }
    }
}
