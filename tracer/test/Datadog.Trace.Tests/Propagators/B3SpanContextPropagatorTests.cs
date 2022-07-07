// <copyright file="B3SpanContextPropagatorTests.cs" company="Datadog">
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
    public class B3SpanContextPropagatorTests
    {
        private static readonly SpanContextPropagator B3Propagator;

        static B3SpanContextPropagatorTests()
        {
            B3Propagator = ContextPropagators.GetSpanContextPropagator(new[] { nameof(ContextPropagators.Names.B3) }, new[] { nameof(ContextPropagators.Names.B3) });
        }

        [Fact]
        public void Inject_IHeadersCollection()
        {
            ulong traceId = 123456789;
            ulong spanId = 987654321;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, null);
            var headers = new Mock<IHeadersCollection>();

            B3Propagator.Inject(context, headers.Object);

            headers.Verify(h => h.Set(B3ContextPropagator.TraceId, "00000000075bcd15"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.SpanId, "000000003ade68b1"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.Sampled, "1"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract sampling from trace context
            var newContext = new SpanContext(null, new TraceContext(null), null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(newContext, newHeaders.Object);
            newHeaders.Verify(h => h.Set(B3ContextPropagator.TraceId, "00000000075bcd15"), Times.Once());
            newHeaders.Verify(h => h.Set(B3ContextPropagator.SpanId, "000000003ade68b1"), Times.Once());
            newHeaders.Verify(h => h.Set(B3ContextPropagator.Sampled, "0"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            var traceContextSamplingField = typeof(TraceContext).GetField("_samplingPriority", BindingFlags.Instance | BindingFlags.NonPublic);
            traceContextSamplingField.SetValue(newContext.TraceContext, SamplingPriorityValues.UserKeep);
            newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(newContext, newHeaders.Object);
            newHeaders.Verify(h => h.Set(B3ContextPropagator.TraceId, "00000000075bcd15"), Times.Once());
            newHeaders.Verify(h => h.Set(B3ContextPropagator.SpanId, "000000003ade68b1"), Times.Once());
            newHeaders.Verify(h => h.Set(B3ContextPropagator.Sampled, "1"), Times.Once());
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

            B3Propagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set(B3ContextPropagator.TraceId, "00000000075bcd15"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.SpanId, "000000003ade68b1"), Times.Once());
            headers.Verify(h => h.Set(B3ContextPropagator.Sampled, "1"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract sampling from trace context
            var newContext = new SpanContext(null, new TraceContext(null), null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(newContext, newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));
            newHeaders.Verify(h => h.Set(B3ContextPropagator.TraceId, "00000000075bcd15"), Times.Once());
            newHeaders.Verify(h => h.Set(B3ContextPropagator.SpanId, "000000003ade68b1"), Times.Once());
            newHeaders.Verify(h => h.Set(B3ContextPropagator.Sampled, "0"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            var traceContextSamplingField = typeof(TraceContext).GetField("_samplingPriority", BindingFlags.Instance | BindingFlags.NonPublic);
            traceContextSamplingField.SetValue(newContext.TraceContext, SamplingPriorityValues.UserKeep);
            newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(newContext, newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));
            newHeaders.Verify(h => h.Set(B3ContextPropagator.TraceId, "00000000075bcd15"), Times.Once());
            newHeaders.Verify(h => h.Set(B3ContextPropagator.SpanId, "000000003ade68b1"), Times.Once());
            newHeaders.Verify(h => h.Set(B3ContextPropagator.Sampled, "1"), Times.Once());
            newHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void Extract_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { "000000000000000000000000075bcd15" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

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
        public void Extract_CarrierAndDelegate()
        {
            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { "000000000000000000000000075bcd15" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object, (carrier, name) => carrier.GetValues(name));

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
        public void ExtractAndInject_PreserveOriginalTraceId()
        {
            var traceId = "0af7651916cd43dd8448eb211c80319c";
            var spanId = "00f067aa0ba902b7";

            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { traceId });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { spanId });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });
            var result = B3Propagator.Extract(headers.Object);

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
            headersForInjection.Setup(h => h.Set(B3ContextPropagator.TraceId, traceId));
            headersForInjection.Setup(h => h.Set(B3ContextPropagator.SpanId, spanId));
            headersForInjection.Setup(h => h.Set(B3ContextPropagator.Sampled, "1"));

            B3Propagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set(B3ContextPropagator.TraceId, traceId), Times.Once());
            headersForInjection.Verify(h => h.Set(B3ContextPropagator.SpanId, spanId), Times.Once());
            headersForInjection.Verify(h => h.Set(B3ContextPropagator.Sampled, "1"), Times.Once());
        }

        [Fact]
        public void Extract_InvalidLength()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { "4321000000000000000000000000075bcd15" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(B3ContextPropagator.TraceId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.SpanId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.Sampled), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_InvalidFormat()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { "432100000000000000000==00000075bcd15" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { "00000+003ade68b1" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(B3ContextPropagator.TraceId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.SpanId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.Sampled), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_EmptyTraceId()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { "          " });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(B3ContextPropagator.TraceId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.SpanId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.Sampled), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_EmptySpanId()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { "000000000000000000000000075bcd15" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { "       " });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(B3ContextPropagator.TraceId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.SpanId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.Sampled), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_InvalidTraceId()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { "00000000000000000000000000000000" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(B3ContextPropagator.TraceId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.SpanId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.Sampled), Times.Once());
            Assert.Null(result);
        }

        [Fact]
        public void Extract_InvalidSpanIdLength()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues(B3ContextPropagator.TraceId))
                   .Returns(new[] { "000000000000000000000000075bcd15" });
            headers.Setup(h => h.GetValues(B3ContextPropagator.SpanId))
                   .Returns(new[] { "    432   " });
            headers.Setup(h => h.GetValues(B3ContextPropagator.Sampled))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues(B3ContextPropagator.TraceId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.SpanId), Times.Once());
            headers.Verify(h => h.GetValues(B3ContextPropagator.Sampled), Times.Once());
            Assert.Null(result);
        }
    }
}
