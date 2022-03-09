// <copyright file="MultiSpanContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class MultiSpanContextPropagatorTests
    {
        private static readonly SpanContextPropagator Propagator;

        static MultiSpanContextPropagatorTests()
        {
            var names = Enum.GetNames(typeof(ContextPropagators.Names));
            Propagator = ContextPropagators.GetSpanContextPropagator(names, names);
        }

        [Fact]
        public void Inject_All_IHeadersCollection()
        {
            ulong traceId = 123456789;
            ulong spanId = 987654321;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, null);
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            headers.Verify(h => h.Set(HttpHeaderNames.TraceId, "123456789"), Times.Once());
            headers.Verify(h => h.Set(HttpHeaderNames.ParentId, "987654321"), Times.Once());
            headers.Verify(h => h.Set(HttpHeaderNames.SamplingPriority, "2"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.TraceId, "00000000075bcd15"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.SpanId, "000000003ade68b1"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.Sampled, "1"), Times.Once());
            headers.Verify(h => h.Set(B3SingleHeaderContextPropagator.B3, "00000000075bcd15-000000003ade68b1-1"), Times.Once());
            headers.Verify(h => h.Set(W3CContextPropagator.TraceParent, "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_All_CarrierAndDelegate()
        {
            ulong traceId = 123456789;
            ulong spanId = 987654321;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, null);

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set(HttpHeaderNames.TraceId, "123456789"), Times.Once());
            headers.Verify(h => h.Set(HttpHeaderNames.ParentId, "987654321"), Times.Once());
            headers.Verify(h => h.Set(HttpHeaderNames.SamplingPriority, "2"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.TraceId, "00000000075bcd15"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.SpanId, "000000003ade68b1"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.Sampled, "1"), Times.Once());
            headers.Verify(h => h.Set(B3SingleHeaderContextPropagator.B3, "00000000075bcd15-000000003ade68b1-1"), Times.Once());
            headers.Verify(h => h.Set(W3CContextPropagator.TraceParent, "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Extract_B3_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { "000000000000000000000000075bcd15" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });

            var result = Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(B3ContextPropagator.TraceId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.SpanId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.Sampled), Times.Once());
            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId = 123456789,
                           SpanId = 987654321,
                           Origin = null,
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                       });
        }

        [Fact]
        public void Extract_B3SingleHeader_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues(B3SingleHeaderContextPropagator.B3))
                   .Returns(new[] { "00000000075bcd15-000000003ade68b1-1" });

            var result = Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(B3SingleHeaderContextPropagator.B3), Times.Once());
            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId = 123456789,
                           SpanId = 987654321,
                           Origin = null,
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                       });
        }

        [Fact]
        public void Extract_W3C_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            var result = Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(W3CContextPropagator.TraceParent), Times.Once());
            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId = 123456789,
                           SpanId = 987654321,
                           Origin = null,
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                       });
        }

        [Fact]
        public void Extract_Datadog_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues(HttpHeaderNames.TraceId))
                   .Returns(new[] { "123456789" });
            headers.Setup(h => h.GetValues(HttpHeaderNames.ParentId))
                   .Returns(new[] { "987654321" });
            headers.Setup(h => h.GetValues(HttpHeaderNames.SamplingPriority))
                   .Returns(new[] { "1" });

            var result = Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(HttpHeaderNames.TraceId), Times.Once());
            headers.Verify(h => h.GetValues(HttpHeaderNames.ParentId), Times.Once());
            headers.Verify(h => h.GetValues(HttpHeaderNames.SamplingPriority), Times.Once());
            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId = 123456789,
                           SpanId = 987654321,
                           Origin = null,
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                       });
        }

        [Fact]
        public void ExtractAndInject_W3C_PreserveOriginalTraceId()
        {
            var traceId = "0af7651916cd43dd8448eb211c80319c";
            var spanId = "00f067aa0ba902b7";
            var expectedTraceParent = $"00-{traceId}-{spanId}-01";
            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { expectedTraceParent });

            var result = Propagator.Extract(headers.Object);

            // 64 bits verify
            var expectedTraceId = 9532127138774266268UL;
            var expectedSpanId = 67667974448284343UL;
            Assert.Equal(expectedTraceId, result.TraceId);
            Assert.Equal(expectedSpanId, result.SpanId);

            // Check truncation
            var truncatedTraceId64 = expectedTraceId.ToString("x16");
            Assert.Equal(truncatedTraceId64, traceId.Substring(16));

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>();
            headersForInjection.Setup(h => h.Set(W3CContextPropagator.TraceParent, expectedTraceParent));

            Propagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set(W3CContextPropagator.TraceParent, expectedTraceParent), Times.Once());
        }

        [Fact]
        public void ExtractAndInject_B3_PreserveOriginalTraceId()
        {
            var traceId = "0af7651916cd43dd8448eb211c80319c";
            var spanId = "00f067aa0ba902b7";

            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { traceId });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { spanId });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });
            var result = Propagator.Extract(headers.Object);

            // 64 bits verify
            var expectedTraceId = 9532127138774266268UL;
            var expectedSpanId = 67667974448284343UL;
            Assert.Equal(expectedTraceId, result.TraceId);
            Assert.Equal(expectedSpanId, result.SpanId);

            // Check truncation
            var truncatedTraceId64 = expectedTraceId.ToString("x16");
            Assert.Equal(truncatedTraceId64, traceId.Substring(16));

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>();
            headersForInjection.Setup(h => h.Set(B3ContextPropagator.TraceId, traceId));
            headersForInjection.Setup(h => h.Set(B3ContextPropagator.SpanId, spanId));
            headersForInjection.Setup(h => h.Set(B3ContextPropagator.Sampled, "1"));

            Propagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set(B3ContextPropagator.TraceId, traceId), Times.Once());
            headersForInjection.Verify(h => h.Set(B3ContextPropagator.SpanId, spanId), Times.Once());
            headersForInjection.Verify(h => h.Set(B3ContextPropagator.Sampled, "1"), Times.Once());
        }

        [Fact]
        public void ExtractAndInject_B3SingleHeader_PreserveOriginalTraceId()
        {
            var traceId = "0af7651916cd43dd8448eb211c80319c";
            var spanId = "00f067aa0ba902b7";
            var expectedTraceParent = $"{traceId}-{spanId}-1";
            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues(B3SingleHeaderContextPropagator.B3))
                   .Returns(new[] { expectedTraceParent });

            var result = Propagator.Extract(headers.Object);

            // 64 bits verify
            var expectedTraceId = 9532127138774266268UL;
            var expectedSpanId = 67667974448284343UL;
            Assert.Equal(expectedTraceId, result.TraceId);
            Assert.Equal(expectedSpanId, result.SpanId);

            // Check truncation
            var truncatedTraceId64 = expectedTraceId.ToString("x16");
            Assert.Equal(truncatedTraceId64, traceId.Substring(16));

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>();
            headersForInjection.Setup(h => h.Set(B3SingleHeaderContextPropagator.B3, expectedTraceParent));

            Propagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set(B3SingleHeaderContextPropagator.B3, expectedTraceParent), Times.Once());
        }
    }
}
