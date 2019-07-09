using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests
{
    public class SpanTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<IAgentWriter> _writerMock;
        private readonly Tracer _tracer;

        public SpanTests(ITestOutputHelper output)
        {
            _output = output;

            var settings = new TracerSettings();
            _writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            _tracer = new Tracer(settings, _writerMock.Object, samplerMock.Object, null);
        }

        [Fact]
        public void SetTag_KeyValue_KeyValueSet()
        {
            const string key = "Key";
            const string value = "Value";
            var span = _tracer.StartSpan("Operation");
            Assert.Null(span.GetTag(key));

            span.SetTag(key, value);

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<List<Span>>()), Times.Never);
            Assert.Equal(span.GetTag(key), value);
        }

        [Fact]
        public void Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed()
        {
            // The 100 additional milliseconds account for the clock precision
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1).AddMilliseconds(-100);
            var span = _tracer.StartSpan("Operation", startTime: startTime);

            span.Finish();

            Assert.True(span.Duration >= TimeSpan.FromMinutes(1) && span.Duration < TimeSpan.FromMinutes(2));
        }

        [Fact]
        public async Task Finish_NoEndTimeProvided_SpanWriten()
        {
            var span = _tracer.StartSpan("Operation");
            await Task.Delay(TimeSpan.FromMilliseconds(1));
            span.Finish();

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<List<Span>>()), Times.Once);
            Assert.True(span.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void Finish_EndTimeProvided_SpanWritenWithCorrectDuration()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(10);
            var span = _tracer.StartSpan("Operation", startTime: startTime);

            span.Finish(endTime);

            Assert.Equal(endTime - startTime, span.Duration);
        }

        [Fact]
        public void Finish_EndTimeInThePast_DurationIs0()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = DateTime.UtcNow.AddMilliseconds(-10);
            var span = _tracer.StartSpan("Operation", startTime: startTime);

            span.Finish(endTime);

            Assert.Equal(TimeSpan.Zero, span.Duration);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(10)]
        [InlineData(100)]
        public void Accurate_Duration(int minimumSleepMilliseconds)
        {
            // TODO: refactor how we measure time so we can lower this threshold
            const int iterations = 10;
            const int maxDifference = 15;
            TimeSpan totalStopwatchTime = TimeSpan.Zero;
            TimeSpan totalSpanTime = TimeSpan.Zero;
            Span span;
            var stopwatch = new Stopwatch();

            // execute once to ensure JIT compilation
            using (span = _tracer.StartSpan("Operation"))
            {
                stopwatch.Restart();
                Thread.Sleep(1);
                var spanMilliseconds = span.Duration.TotalMilliseconds;
                var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            }

            // execute multiple times and average the results
            for (int x = 0; x < iterations; x++)
            {
                using (span = _tracer.StartSpan("Operation"))
                {
                    stopwatch.Restart();
                    Thread.Sleep(minimumSleepMilliseconds);
                    totalStopwatchTime += stopwatch.Elapsed;
                }

                totalSpanTime += span.Duration;
            }

            var avgStopwatch = totalStopwatchTime.TotalMilliseconds / iterations;
            var avgSpan = totalSpanTime.TotalMilliseconds / iterations;
            var stopwatchSpanDiff = Math.Abs(avgSpan - avgStopwatch);
            // The difference should be less than the threshold
            _output.WriteLine($"Average elapsed time: {avgStopwatch:0.0} ms");
            _output.WriteLine($"Average span time: {avgSpan:0.0} ms");
            Assert.True(
                stopwatchSpanDiff < maxDifference,
                $"Span duration difference ({stopwatchSpanDiff}ms) outside of allowed threshold {maxDifference}ms. Expected average: {avgStopwatch}ms, actual average: {avgSpan}ms");
        }
    }
}
