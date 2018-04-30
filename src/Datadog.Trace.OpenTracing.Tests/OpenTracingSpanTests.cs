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
        private OpenTracingTracer _tracer;

        public OpenTracingSpanTests()
        {
            _writerMock = new Mock<IAgentWriter>();
            var ddTracer = new Tracer(_writerMock.Object);
            _tracer = new OpenTracingTracer(ddTracer);
        }

        [Fact]
        public void SetTag_Tags_TagsAreProperlySet()
        {
            var scope = (OpenTracingScope)_tracer.BuildSpan("Op1")
                                                 .StartActive(finishSpanOnDispose: true);
            var span = scope.Span;

            span.SetTag("StringKey", "What's tracing");
            span.SetTag("IntKey", 42);
            span.SetTag("DoubleKey", 1.618);
            span.SetTag("BoolKey", true);

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<List<Span>>()), Times.Never);
            Assert.Equal("What's tracing", span.DatadogSpan.GetTag("StringKey"));
            Assert.Equal("42", span.DatadogSpan.GetTag("IntKey"));
            Assert.Equal("1.618", span.DatadogSpan.GetTag("DoubleKey"));
            Assert.Equal("True", span.DatadogSpan.GetTag("BoolKey"));
        }

        [Fact]
        public void SetOperationName_ValidOperationName_OperationNameIsProperlySet()
        {
            var scope = (OpenTracingScope)_tracer.BuildSpan("Op1")
                                                 .StartActive(finishSpanOnDispose: true);
            var span = scope.Span;

            span.SetOperationName("Op1");

            Assert.Equal("Op1", span.DatadogSpan.OperationName);
        }

        [Fact]
        public async Task Finish_NoEndTimeProvided_SpanWriten()
        {
            var scope = (OpenTracingScope)_tracer.BuildSpan("Op1")
                                                 .StartActive(finishSpanOnDispose: true);
            var span = scope.Span;

            await Task.Delay(TimeSpan.FromMilliseconds(1));
            span.Finish();

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<List<Span>>()), Times.Once);
            Assert.True(span.DatadogSpan.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void Finish_EndTimeProvided_SpanWritenWithCorrectDuration()
        {
            var duration = TimeSpan.FromSeconds(1);

            var scope = (OpenTracingScope)_tracer.BuildSpan("Op1")
                                                 .StartActive(finishSpanOnDispose: true);
            var span = scope.Span;
            span.Finish(span.DatadogSpan.StartTime + duration);

            Assert.Equal(duration, span.DatadogSpan.Duration);
        }

        [Fact]
        public void Finish_EndTimeInThePast_DurationIs0()
        {
            var scope = (OpenTracingScope)_tracer.BuildSpan("Op1")
                                                 .StartActive(finishSpanOnDispose: true);
            var span = scope.Span;

            span.Finish(DateTimeOffset.UtcNow.AddMinutes(-1));

            Assert.Equal(TimeSpan.Zero, span.DatadogSpan.Duration);
        }

        [Fact]
        public void Dispose_ExitUsing_SpanWriten()
        {
            var scope = (OpenTracingScope)_tracer.BuildSpan("Op1")
                                                 .StartActive(finishSpanOnDispose: true);
            var span = scope.Span;
            span.Finish();

            Assert.True(span.DatadogSpan.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void Context_TwoCalls_ContextStaysEqual()
        {
            var scope = _tracer.BuildSpan("Op1")
                               .StartActive(finishSpanOnDispose: true);
            var span = scope.Span;

            Assert.Same(span.Context, span.Context);
        }
    }
}