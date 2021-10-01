// <copyright file="OpenTracingSpanTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using NUnit.Framework;
using OpenTracing;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class OpenTracingSpanTests
    {
        private OpenTracingTracer _tracer;

        [SetUp]
        public void Before()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            var datadogTracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
            _tracer = new OpenTracingTracer(datadogTracer);
        }

        [Test]
        public void SetTag_Tags_TagsAreProperlySet()
        {
            ISpan span = GetScope("Op1").Span;

            span.SetTag("StringKey", "What's tracing");
            span.SetTag("IntKey", 42);
            span.SetTag("DoubleKey", 1.618);
            span.SetTag("BoolKey", true);

            var otSpan = (OpenTracingSpan)span;
            Assert.AreEqual("What's tracing", otSpan.GetTag("StringKey"));
            Assert.AreEqual("42", otSpan.GetTag("IntKey"));
            Assert.AreEqual("1.618", otSpan.GetTag("DoubleKey"));
            Assert.AreEqual("True", otSpan.GetTag("BoolKey"));
        }

        [Test]
        public void SetTag_SpecialTags_ServiceNameSetsService()
        {
            ISpan span = GetScope("Op1").Span;
            const string value = "value";

            span.SetTag(DatadogTags.ServiceName, value);

            var otSpan = (OpenTracingSpan)span;
            Assert.AreEqual(value, otSpan.Span.ServiceName);
        }

        [Test]
        public void SetTag_SpecialTags_ServiceVersionSetsVersion()
        {
            ISpan span = GetScope("Op1").Span;
            const string value = "value";

            span.SetTag(DatadogTags.ServiceVersion, value);

            var otSpan = (OpenTracingSpan)span;
            Assert.AreEqual(value, otSpan.GetTag(Tags.Version));
            Assert.AreEqual(value, otSpan.GetTag(DatadogTags.ServiceVersion));
        }

        [Test]
        public void SetOperationName_ValidOperationName_OperationNameIsProperlySet()
        {
            ISpan span = GetScope("Op0").Span;

            span.SetOperationName("Op1");

            Assert.AreEqual("Op1", ((OpenTracingSpan)span).OperationName);
        }

        [Test]
        public void Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed()
        {
            TimeSpan expectedDuration = TimeSpan.FromMinutes(1);
            var startTime = DateTimeOffset.UtcNow - expectedDuration;

            ISpan span = GetScope("Op1", startTime).Span;
            span.Finish();

            double durationDifference = Math.Abs((((OpenTracingSpan)span).Duration - expectedDuration).TotalMilliseconds);
            Assert.True(durationDifference < 100);
        }

        /*
        [Test]
        public async Task Finish_NoEndTimeProvided_SpanWriten()
        {
            var span = new OpenTracingSpan(_tracer.StartActive(null, null, null, null));
            await Task.Delay(TimeSpan.FromMilliseconds(1));
            span.Finish();

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<List<Span>>()), Times.Once);
            Assert.True(span.DDSpan.Duration > TimeSpan.Zero);
        }
        */

        [Test]
        public void Finish_EndTimeProvided_SpanWritenWithCorrectDuration()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = startTime.AddMilliseconds(10);

            ISpan span = GetScope("Op1", startTime).Span;
            span.Finish(endTime);

            Assert.AreEqual(endTime - startTime, ((OpenTracingSpan)span).Duration);
        }

        [Test]
        public void Finish_EndTimeInThePast_DurationIs0()
        {
            var startTime = DateTimeOffset.UtcNow;
            var endTime = startTime.AddMilliseconds(-10);

            ISpan span = GetScope("Op1", startTime).Span;
            span.Finish(endTime);

            Assert.AreEqual(TimeSpan.Zero, ((OpenTracingSpan)span).Duration);
        }

        [Test]
        public void Dispose_ExitUsing_SpanWriten()
        {
            OpenTracingSpan span;

            using (IScope scope = GetScope("Op1"))
            {
                span = (OpenTracingSpan)scope.Span;
            }

            Assert.True(span.Duration > TimeSpan.Zero);
        }

        [Test]
        public void Context_TwoCalls_ContextStaysEqual()
        {
            ISpan span;
            global::OpenTracing.ISpanContext firstContext;

            using (IScope scope = GetScope("Op1"))
            {
                span = scope.Span;
                firstContext = span.Context;
            }

            var secondContext = span.Context;

            Assert.AreSame(firstContext, secondContext);
        }

        private IScope GetScope(string operationName, DateTimeOffset? startTime = null)
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
