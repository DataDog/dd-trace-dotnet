// <copyright file="W3CSpanContextPropagatorTests.cs" company="Datadog">
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
    public class W3CSpanContextPropagatorTests
    {
        private static readonly SpanContextPropagator W3CPropagator;

        static W3CSpanContextPropagatorTests()
        {
            W3CPropagator = ContextPropagators.GetSpanContextPropagator(new[] { nameof(ContextPropagators.Names.W3C) }, new[] { nameof(ContextPropagators.Names.W3C) });
        }

        [Fact]
        public void Inject_IHeadersCollection()
        {
            ulong traceId = 123456789;
            ulong spanId = 987654321;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, null);
            var headers = new Mock<IHeadersCollection>();

            W3CPropagator.Inject(context, headers.Object);

            headers.Verify(h => h.Set(W3CContextPropagator.TraceParent, "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract sampling from trace context
            var newContext = new SpanContext(null, new TraceContext(null), null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();
            W3CPropagator.Inject(newContext, newHeaders.Object);
            newHeaders.Verify(h => h.Set(W3CContextPropagator.TraceParent, "00-000000000000000000000000075bcd15-000000003ade68b1-00"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            var traceContextSamplingField = typeof(TraceContext).GetField("_samplingPriority", BindingFlags.Instance | BindingFlags.NonPublic);
            traceContextSamplingField.SetValue(newContext.TraceContext, SamplingPriorityValues.UserKeep);
            newHeaders = new Mock<IHeadersCollection>();
            W3CPropagator.Inject(newContext, newHeaders.Object);
            newHeaders.Verify(h => h.Set(W3CContextPropagator.TraceParent, "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            newHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            ulong traceId = 123456789;
            ulong spanId = 987654321;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, null);

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            W3CPropagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set(W3CContextPropagator.TraceParent, "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract sampling from trace context
            var newContext = new SpanContext(null, new TraceContext(null), null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();
            W3CPropagator.Inject(newContext, newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));
            newHeaders.Verify(h => h.Set(W3CContextPropagator.TraceParent, "00-000000000000000000000000075bcd15-000000003ade68b1-00"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            var traceContextSamplingField = typeof(TraceContext).GetField("_samplingPriority", BindingFlags.Instance | BindingFlags.NonPublic);
            traceContextSamplingField.SetValue(newContext.TraceContext, SamplingPriorityValues.UserKeep);
            newHeaders = new Mock<IHeadersCollection>();
            W3CPropagator.Inject(newContext, newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));
            newHeaders.Verify(h => h.Set(W3CContextPropagator.TraceParent, "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            newHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void Extract_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            var result = W3CPropagator.Extract(headers.Object);

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
        public void Extract_CarrierAndDelegate()
        {
            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            var result = W3CPropagator.Extract(headers.Object, (carrier, name) => carrier.GetValues(name));

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
        public void ExtractAndInject_PreserveOriginalTraceId()
        {
            var traceId = "0af7651916cd43dd8448eb211c80319c";
            var spanId = "00f067aa0ba902b7";
            var expectedTraceParent = $"00-{traceId}-{spanId}-01";
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { expectedTraceParent });

            var result = W3CPropagator.Extract(headers.Object);

            // 64 bits verify
            var expectedTraceId = 9532127138774266268UL;
            var expectedSpanId = 67667974448284343UL;
            Assert.Equal(expectedTraceId, result.TraceId);
            Assert.Equal(expectedSpanId, result.SpanId);

            // Check truncation
            var truncatedTraceId64 = expectedTraceId.ToString("x16");
            Assert.Equal(truncatedTraceId64, traceId.Substring(16));

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headersForInjection.Setup(h => h.Set(W3CContextPropagator.TraceParent, expectedTraceParent));

            W3CPropagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set(W3CContextPropagator.TraceParent, expectedTraceParent), Times.Once());
        }

        [Fact]
        public void Extract_InvalidLength()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { "00-1234000000000000000000000000075bcd15-000000003ade68b1-01" });

            var result = W3CPropagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(W3CContextPropagator.TraceParent), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_InvalidFormat()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { "00=000000000000000000000000075bcd15=000000003ade68b1=01" });

            var result = W3CPropagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(W3CContextPropagator.TraceParent), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_InvalidSampledFormat()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-51" });

            var result = W3CPropagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(W3CContextPropagator.TraceParent), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_EmptyTraceIdStrings()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { "00-                                -000000003ade68b1-01" });

            var result = W3CPropagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(W3CContextPropagator.TraceParent), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_EmptyStrings()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(W3CContextPropagator.TraceParent))
                   .Returns(new[] { "       " });

            var result = W3CPropagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(W3CContextPropagator.TraceParent), Times.Once());
            Assert.Null(result);
        }
    }
}
