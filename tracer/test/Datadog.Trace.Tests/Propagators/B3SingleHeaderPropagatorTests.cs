// <copyright file="B3SingleHeaderPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class B3SingleHeaderPropagatorTests
    {
        private static readonly SpanContextPropagator B3Propagator;

        static B3SingleHeaderPropagatorTests()
        {
            B3Propagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[] { ContextPropagationHeaderStyle.B3SingleHeader },
                new[] { ContextPropagationHeaderStyle.B3SingleHeader });
        }

        [Fact]
        public void Inject_IHeadersCollection()
        {
            var traceId = (TraceId)123456789;
            const ulong spanId = 987654321;
            const int samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, null);
            var headers = new Mock<IHeadersCollection>();

            B3Propagator.Inject(context, headers.Object);

            headers.Verify(h => h.Set("b3", "00000000075bcd15-000000003ade68b1-1"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract sampling from trace context
            var newContext = new SpanContext(parent: null, new TraceContext(null), serviceName: null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(newContext, newHeaders.Object);
            newHeaders.Verify(h => h.Set("b3", "00000000075bcd15-000000003ade68b1-0"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            var traceContextSamplingField = typeof(TraceContext).GetField("_samplingPriority", BindingFlags.Instance | BindingFlags.NonPublic);
            traceContextSamplingField.SetValue(newContext.TraceContext, SamplingPriorityValues.UserKeep);
            newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(newContext, newHeaders.Object);
            newHeaders.Verify(h => h.Set("b3", "00000000075bcd15-000000003ade68b1-1"), Times.Once());
            newHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            var traceId = (TraceId)123456789;
            ulong spanId = 987654321;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, null);

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            B3Propagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set("b3", "00000000075bcd15-000000003ade68b1-1"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract sampling from trace context
            var newContext = new SpanContext(parent: null, new TraceContext(null), serviceName: null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(newContext, newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));
            newHeaders.Verify(h => h.Set("b3", "00000000075bcd15-000000003ade68b1-0"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            var traceContextSamplingField = typeof(TraceContext).GetField("_samplingPriority", BindingFlags.Instance | BindingFlags.NonPublic);
            traceContextSamplingField.SetValue(newContext.TraceContext, SamplingPriorityValues.UserKeep);
            newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(newContext, newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));
            newHeaders.Verify(h => h.Set("b3", "00000000075bcd15-000000003ade68b1-1"), Times.Once());
            newHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void Extract_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { "00000000075bcd15-000000003ade68b1-1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("b3"), Times.Once());
            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = (TraceId)123456789,
                           TraceId = 123456789,
                           SpanId = 987654321,
                           RawTraceId = "00000000075bcd15",
                           RawSpanId = "000000003ade68b1",
                           Origin = null,
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                       });
        }

        [Fact]
        public void Extract_CarrierAndDelegate()
        {
            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { "00000000075bcd15-000000003ade68b1-1" });

            var result = B3Propagator.Extract(headers.Object, (carrier, name) => carrier.GetValues(name));

            headers.Verify(h => h.GetValues("b3"), Times.Once());

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = (TraceId)123456789,
                           TraceId = 123456789,
                           SpanId = 987654321,
                           RawTraceId = "00000000075bcd15",
                           RawSpanId = "000000003ade68b1",
                           Origin = null,
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                       });
        }

        [Fact]
        public void ExtractAndInject_PreserveOriginalTraceId()
        {
            var traceId = "0af7651916cd43dd8448eb211c80319c";
            var spanId = "00f067aa0ba902b7";
            var expectedTraceParent = $"{traceId}-{spanId}-1";
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { expectedTraceParent });

            var result = B3Propagator.Extract(headers.Object);

            // 64 bits verify
            var expectedTraceId = (TraceId)9532127138774266268UL;
            var expectedSpanId = 67667974448284343UL;
            result.TraceId128.Should().Be(expectedTraceId);
            result.SpanId.Should().Be(expectedSpanId);

            // Check truncation
            var truncatedTraceId64 = expectedTraceId.Lower.ToString("x16");
            traceId.Substring(16).Should().Be(truncatedTraceId64);

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>();
            headersForInjection.Setup(h => h.Set("b3", expectedTraceParent));

            B3Propagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set("b3", expectedTraceParent), Times.Once());
        }

        [Fact]
        public void Extract_InvalidLength()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { "242300000000075bcd15-000000003ade68b1-1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("b3"), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_InvalidFormat()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { "00000000075bcd15=000000003ade68b1=1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("b3"), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_EmptyStrings()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { "                                   " });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("b3"), Times.Once());
            Assert.Null(result);
        }
    }
}
