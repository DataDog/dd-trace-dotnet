// <copyright file="OpenTracingSpanTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using OpenTracing;
using Xunit;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class OpenTracingSpanTests
    {
        private readonly OpenTracingTracer _tracer;

        public OpenTracingSpanTests()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            var datadogTracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
            _tracer = new OpenTracingTracer(datadogTracer);
        }

        [Fact]
        public void SetTag_Tags_TagsAreProperlySet()
        {
            var span = GetScope("Op1").Span;

            span.SetTag("StringKey", "What's tracing");
            span.SetTag("IntKey", 42);
            span.SetTag("DoubleKey", 1.618);
            span.SetTag("BoolKey", true);

            var otSpan = (OpenTracingSpan)span;
            Assert.Equal("What's tracing", otSpan.GetTag("StringKey"));
            Assert.Equal("42", otSpan.GetTag("IntKey"));
            Assert.Equal("1.618", otSpan.GetTag("DoubleKey"));
            Assert.Equal("True", otSpan.GetTag("BoolKey"));
        }

        [Fact]
        public void SetTag_SpecialTags_ServiceNameSetsService()
        {
            var span = GetScope("Op1").Span;
            const string value = "value";

            span.SetTag(DatadogTags.ServiceName, value);

            var otSpan = (OpenTracingSpan)span;
            Assert.Equal(value, otSpan.Span.ServiceName);
        }

        [Fact]
        public void SetTag_SpecialTags_ServiceVersionSetsVersion()
        {
            var span = GetScope("Op1").Span;
            const string value = "value";

            span.SetTag(DatadogTags.ServiceVersion, value);

            var otSpan = (OpenTracingSpan)span;
            Assert.Equal(value, otSpan.GetTag(Tags.Version));
            Assert.Equal(value, otSpan.GetTag(DatadogTags.ServiceVersion));
        }

        [Fact]
        public void SetOperationName_ValidOperationName_OperationNameIsProperlySet()
        {
            var span = GetScope("Op0").Span;

            span.SetOperationName("Op1");

            Assert.Equal("Op1", ((OpenTracingSpan)span).OperationName);
        }

        [Fact]
        public void Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed()
        {
            TimeSpan expectedDuration = TimeSpan.FromMinutes(1);
            var startTime = DateTimeOffset.UtcNow - expectedDuration;

            var span = GetScope("Op1", startTime).Span;
            span.Finish();

            var otSpan = (OpenTracingSpan)span;
            var ddSpan = (Span)otSpan.Span;
            double durationDifference = Math.Abs((ddSpan.Duration - expectedDuration).TotalMilliseconds);
            Assert.True(durationDifference < 100);
        }

        /*
        [Fact]
        public async Task Finish_NoEndTimeProvided_SpanWriten()
        {
            var span = new OpenTracingSpan(_tracer.StartActive(null, null, null, null));
            await Task.Delay(TimeSpan.FromMilliseconds(1));
            span.Finish();

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<List<Span>>()), Times.Once);
            Assert.True(span.DDSpan.Duration > TimeSpan.Zero);
        }
        */

        [Fact]
        public void Finish_EndTimeProvided_SpanWritenWithCorrectDuration()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = startTime.AddMilliseconds(10);

            var span = GetScope("Op1", startTime).Span;
            span.Finish(endTime);

            var otSpan = (OpenTracingSpan)span;
            var ddSpan = (Span)otSpan.Span;
            Assert.Equal(endTime - startTime, ddSpan.Duration);
        }

        [Fact]
        public void Finish_EndTimeInThePast_DurationIs0()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = startTime.AddMilliseconds(-10);

            var span = GetScope("Op1", startTime).Span;
            span.Finish(endTime);

            var otSpan = (OpenTracingSpan)span;
            var ddSpan = (Span)otSpan.Span;
            Assert.Equal(TimeSpan.Zero, ddSpan.Duration);
        }

        [Fact]
        public void Dispose_ExitUsing_SpanWriten()
        {
            OpenTracingSpan span;

            using (var scope = GetScope("Op1"))
            {
                span = (OpenTracingSpan)scope.Span;
            }

            var ddSpan = (Span)span.Span;
            Assert.True(ddSpan.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void Context_TwoCalls_ContextStaysEqual()
        {
            global::OpenTracing.ISpan span;
            global::OpenTracing.ISpanContext firstContext;

            using (var scope = GetScope("Op1"))
            {
                span = scope.Span;
                firstContext = span.Context;
            }

            var secondContext = span.Context;

            Assert.Same(firstContext, secondContext);
        }

        private global::OpenTracing.IScope GetScope(string operationName, DateTimeOffset? startTime = null)
        {
            ISpanBuilder spanBuilder = new OpenTracingSpanBuilder(_tracer, operationName);

            if (startTime != null)
            {
                spanBuilder = spanBuilder.WithStartTimestamp(startTime.Value);
            }

            return spanBuilder.StartActive(finishSpanOnDispose: true);
        }
    }
}
