// <copyright file="SpanTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Sampling;
using FluentAssertions;
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

            var settings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.PeerServiceDefaultsEnabled, "true" },
                { ConfigurationKeys.PeerServiceNameMappings, "a-peer-service:a-remmaped-peer-service" }
            });

            _writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();

            _tracer = new Tracer(settings, _writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        }

        [Fact]
        public void SetTag_KeyValue_KeyValueSet()
        {
            const string key = "Key";
            const string value = "Value";
            var span = _tracer.StartSpan("Operation");
            Assert.Null(span.GetTag(key));

            span.SetTag(key, value);

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<ArraySegment<Span>>()), Times.Never);
            span.GetTag(key).Should().Be(value);
        }

        [Fact]
        public void SetPeerServiceTag_CallsRemapper()
        {
            var span = _tracer.StartSpan("Operation");
            Assert.Null(span.GetTag(Tags.PeerService));

            span.SetTag(Tags.PeerService, "a-peer-service");

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<ArraySegment<Span>>()), Times.Never);
            span.GetTag(Tags.PeerService).Should().Be("a-remmaped-peer-service");
            span.GetTag(Tags.PeerServiceRemappedFrom).Should().Be("a-peer-service");
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

            _writerMock.Verify(x => x.WriteTrace(It.IsAny<ArraySegment<Span>>()), Times.Once);
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

        [Fact]
        public void Accurate_Duration()
        {
            // Check that span has the same precision as stopwatch
            // The reasoning behind the test is: let's imagine that Span uses DateTime internally, with a 15 ms precision.
            // Depending on how it's implemented, for a time of 5 ms it will report either 0 ms or 15 ms.
            // If the former, then the first assertion will fail.If the latter, then the second assertion will fail.
            const int iterations = 10;

            // Sleeps just the right amount of time to be sure we do not exceed the precision of the stopwatch
            // Not using Thread.Sleep(1) because it has the same precision as DateTime, so it could skew the test
            static void Sleep()
            {
                var timestamp = Stopwatch.GetTimestamp();

                while (timestamp == Stopwatch.GetTimestamp())
                {
                    Thread.SpinWait(1);
                }
            }

            var stopwatch = new Stopwatch();

            for (int i = 0; i < iterations; i++)
            {
                Span span;

                using (span = _tracer.StartSpan("Operation"))
                {
                    stopwatch.Restart();
                    Sleep();
                    stopwatch.Stop();
                }

                span.Duration.Should().BeGreaterOrEqualTo(stopwatch.Elapsed);

                stopwatch.Restart();
                using (span = _tracer.StartSpan("Operation"))
                {
                    Sleep();
                }

                stopwatch.Stop();

                span.Duration.Should().BeLessOrEqualTo(stopwatch.Elapsed);
            }
        }

        [Fact]
        public void TopLevelSpans()
        {
            // local function to start active scope and set span's service name
            Scope StartActive(string operationName, string serviceName)
            {
                var scope = (Scope)_tracer.StartActive(operationName);
                scope.Span.ServiceName = serviceName;
                return scope;
            }

            var spans = new List<(Scope Scope, bool IsTopLevel)>();

            spans.Add((StartActive(operationName: "Root", serviceName: "root"), true));
            spans.Add((StartActive(operationName: "Child1", serviceName: "root"), false));
            spans.Add((StartActive(operationName: "Child2", serviceName: "child"), true));
            spans.Add((StartActive(operationName: "Child3", serviceName: "child"), false));
            spans.Add((StartActive(operationName: "Child4", serviceName: "root"), true));

            foreach (var (scope, expectedResult) in spans)
            {
                scope.Span.Should().Match<Span>(s => s.IsTopLevel == expectedResult);
            }
        }

        [Fact]
        public void SpanIds_SingleSpanIsRoot()
        {
            Span span = _tracer.StartSpan("Operation Galactic Storm");
            using (span)
            {
                span.SpanId.Should().NotBe(0);
                span.RootSpanId.Should().Be(span.SpanId);
            }
        }

        [Fact]
        public void SpanIds_SingleScopeIsRoot()
        {
            Scope scope = (Scope)_tracer.StartActive("Operation Galactic Storm");
            var span = scope.Span;
            using (scope)
            {
                span.SpanId.Should().NotBe(0);
                span.RootSpanId.Should().Be(scope.Span.SpanId);
            }
        }

        [Fact]
        public void SpanIds_RemoteParentOfSpanIsNotLocalRoot()
        {
            const ulong remoteParentSpanId = 1234567890123456789;
            SpanContext remoteParentSpanCtx = new SpanContext(traceId: null, spanId: remoteParentSpanId);
            var span = _tracer.StartSpan(operationName: "Operation Galactic Storm", parent: remoteParentSpanCtx);
            using (span)
            {
                span.SpanId.Should().NotBe(0);
                span.SpanId.Should().NotBe(remoteParentSpanId);                 // There is an expected 1 in 2^64 chance of this line failing
                span.RootSpanId.Should().Be(span.SpanId);
            }
        }

        [Fact]
        public void SpanIds_RemoteParentOfScopeIsNotLocalRoot()
        {
            const ulong remoteParentSpanId = 1234567890123456789;
            SpanContext remoteParentSpanCtx = new SpanContext(traceId: null, spanId: remoteParentSpanId);
            var spanCreationSettings = new SpanCreationSettings() { Parent = remoteParentSpanCtx };
            Scope scope = (Scope)_tracer.StartActive(operationName: "Operation Galactic Storm", spanCreationSettings);
            var span = scope.Span;
            using (scope)
            {
                span.SpanId.Should().NotBe(0);
                span.SpanId.Should().NotBe(remoteParentSpanId);           // There is an expected 1 in 2^64 chance of this line failing
                span.RootSpanId.Should().Be(scope.Span.SpanId);
            }
        }

        [Fact]
        public void SpanIds_RootOfSpanHierarchy()
        {
            const ulong remoteParentSpanId = 1234567890123456789;
            SpanContext remoteParentSpanCtx = new SpanContext(traceId: null, spanId: remoteParentSpanId);

            using (Span span1 = _tracer.StartSpan(operationName: "Operation Root", parent: remoteParentSpanCtx))
            using (Span span2 = _tracer.StartSpan(operationName: "Operation Middle", parent: span1.Context))
            using (Span span3 = _tracer.StartSpan(operationName: "Operation Leaf", parent: span2.Context))
            {
                span1.SpanId.Should().NotBe(0);
                span2.SpanId.Should().NotBe(0);
                span3.SpanId.Should().NotBe(0);

                span1.SpanId.Should().NotBe(remoteParentSpanId);                // There is an expected 1 in 2^64 chance of this line failing
                span2.SpanId.Should().NotBe(remoteParentSpanId);                // There is an expected 1 in 2^64 chance of this line failing
                span3.SpanId.Should().NotBe(remoteParentSpanId);                // There is an expected 1 in 2^64 chance of this line failing

                span1.RootSpanId.Should().Be(span1.SpanId);
                span2.RootSpanId.Should().Be(span1.SpanId);
                span3.RootSpanId.Should().Be(span1.SpanId);
            }
        }

        [Fact]
        public void SpanIds_RootOfScopeHierarchy()
        {
            using (Scope scope1 = (Scope)_tracer.StartActive(operationName: "Operation Root"))
            using (Scope scope2 = (Scope)_tracer.StartActive(operationName: "Operation Middle"))
            using (Scope scope3 = (Scope)_tracer.StartActive(operationName: "Operation Leaf"))
            {
                scope1.Span.SpanId.Should().NotBe(0);
                scope2.Span.SpanId.Should().NotBe(0);
                scope3.Span.SpanId.Should().NotBe(0);

                var span1 = scope1.Span;
                var span2 = scope2.Span;
                var span3 = scope3.Span;

                span1.RootSpanId.Should().Be(scope1.Span.SpanId);
                span2.RootSpanId.Should().Be(scope1.Span.SpanId);
                span3.RootSpanId.Should().Be(scope1.Span.SpanId);
            }
        }

        [Fact]
        public void SpanIds_RootOfScopeSpanMixedHierarchy()
        {
            const ulong remoteParentSpanId = 1234567890123456789;
            SpanContext remoteParentSpanCtx = new SpanContext(traceId: null, spanId: remoteParentSpanId);
            var spanCreationSettings = new SpanCreationSettings() { Parent = remoteParentSpanCtx };

            using (Scope scope1 = (Scope)_tracer.StartActive(operationName: "Operation Root", spanCreationSettings))
            using (Span span2 = _tracer.StartSpan(operationName: "Operation Middle 1"))
            using (Scope scope3 = (Scope)_tracer.StartActive(operationName: "Operation Middle 2"))
            using (Span span4 = _tracer.StartSpan(operationName: "Operation Leaf"))
            {
                var span1 = scope1.Span;
                var span3 = scope3.Span;

                span1.SpanId.Should().NotBe(0);
                span2.SpanId.Should().NotBe(0);
                span3.SpanId.Should().NotBe(0);
                span4.SpanId.Should().NotBe(0);

                span1.SpanId.Should().NotBe(remoteParentSpanId);          // There is an expected 1 in 2^64 chance of this line failing
                span2.SpanId.Should().NotBe(remoteParentSpanId);                // There is an expected 1 in 2^64 chance of this line failing
                span3.SpanId.Should().NotBe(remoteParentSpanId);          // There is an expected 1 in 2^64 chance of this line failing
                span4.SpanId.Should().NotBe(remoteParentSpanId);                // There is an expected 1 in 2^64 chance of this line failing

                span1.Context.ParentId.Should().Be(remoteParentSpanId);   // Parent (not root) of S1 is remote
                span2.Context.ParentId.Should().Be(scope1.Span.SpanId);         // Parent of S2 is S1: it was created in an active S1-scope
                span3.Context.ParentId.Should().Be(scope1.Span.SpanId);   // Parent of S3 is also S1: it was created in an active S1 scope, S2 is not a scope
                span4.Context.ParentId.Should().Be(scope3.Span.SpanId);         // Parent of S4 is S3: it was created in an active S3-scope

                span1.RootSpanId.Should().Be(scope1.Span.SpanId);
                span2.RootSpanId.Should().Be(scope1.Span.SpanId);
                span3.RootSpanId.Should().Be(scope1.Span.SpanId);
                span4.RootSpanId.Should().Be(scope1.Span.SpanId);
            }
        }

        [Theory]
        [InlineData(0x1234567890abcdef, 0x1122334455667788, "1234567890abcdef1122334455667788")]
        [InlineData(0, 0x1122334455667788, "00000000000000001122334455667788")]
        public void GetTag_TraceId(ulong upper, ulong lower, string expected)
        {
            var traceId = new TraceId(upper, lower);
            var trace = new TraceContext(Mock.Of<IDatadogTracer>());
            var propagatedContext = new SpanContext(traceId, spanId: 1, samplingPriority: null, serviceName: null, origin: null);
            var childContext = new SpanContext(propagatedContext, trace, serviceName: null);
            var span = new Span(childContext, start: null);

            span.GetTag(Tags.TraceId).Should().Be(expected);
        }

        [Fact]
        public void SetTag_Double()
        {
            var span = _tracer.StartSpan(nameof(SetTag_Double));
            var stringKey = "StringTag";
            var stringValue = "My Tag";
            var numericKey = "NumericValue";
            var numericValue = int.MaxValue;

            // Write a normal string tag.
            span.SetTag(stringKey, stringValue);

            // Let's set the numeric value to the span (save it into the Metrics dictionary)
            span.SetTag(numericKey, numericValue);

            // The normal GetTag only look into the Meta dictionary.
            span.GetTag(stringKey).Should().Be(stringValue);
            span.GetTag(numericKey).Should().BeNull();
        }

        [Fact]
        public void ServiceOverride_WhenSet_HasBaseService()
        {
            var origName = "MyServiceA";
            var newName = "MyServiceB";
            var span = _tracer.StartSpan(nameof(SetTag_Double), serviceName: origName);
            span.ServiceName = newName;

            span.ServiceName.Should().Be(newName);
            span.GetTag(Tags.BaseService).Should().Be(origName);
        }

        [Fact]
        public void ServiceOverride_WhenNotSet_HasNoBaseService()
        {
            var origName = "MyServiceA";
            var span = _tracer.StartSpan(nameof(SetTag_Double), serviceName: origName);

            span.ServiceName.Should().Be(origName);
            span.GetTag(Tags.BaseService).Should().BeNull();
        }

        [Fact]
        public void ServiceOverride_WhenSetSame_HasNoBaseService()
        {
            var origName = "MyServiceA";
            var span = _tracer.StartSpan(nameof(SetTag_Double), serviceName: origName);
            span.ServiceName = origName;

            span.ServiceName.Should().Be(origName);
            span.GetTag(Tags.BaseService).Should().BeNull();
        }

        [Fact]
        public void ServiceOverride_WhenSetSameWithDifferentCase_HasNoBaseService()
        {
            var origName = "MyServiceA";
            var newName = origName.ToUpper();
            var span = _tracer.StartSpan(nameof(SetTag_Double), serviceName: origName);
            span.ServiceName = newName;

            span.ServiceName.Should().Be(newName); // ServiceName should change although _dd.base_service has not been added
            span.GetTag(Tags.BaseService).Should().BeNull();
        }

        [Fact]
        public void ServiceOverride_WhenSetTwice_HasBaseService()
        {
            var origName = "MyServiceA";
            var newName = "MyServiceC";
            var span = _tracer.StartSpan(nameof(SetTag_Double), serviceName: origName);
            span.ServiceName = "MyServiceB";
            span.ServiceName = newName;

            span.ServiceName.Should().Be(newName);
            span.GetTag(Tags.BaseService).Should().Be(origName);
        }
    }
}
