// <copyright file="B3MultipleHeadersPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class B3MultipleHeadersPropagatorTests
    {
        private static readonly SpanContextPropagator B3Propagator;

        static B3MultipleHeadersPropagatorTests()
        {
            B3Propagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
                [ContextPropagationHeaderStyle.B3MultipleHeaders],
                [ContextPropagationHeaderStyle.B3MultipleHeaders],
                false);
        }

        [Fact]
        public void Inject_IHeadersCollection()
        {
            var traceId = new TraceId(0x0123456789abcdef, 0x1122334455667788); // 0x0123456789abcdef1122334455667788
            ulong spanId = 0x000000003ade68b1;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, null);
            var headers = new Mock<IHeadersCollection>();

            B3Propagator.Inject(context, headers.Object);

            headers.Verify(h => h.Set("x-b3-traceid", "0123456789abcdef1122334455667788"), Times.Once());
            headers.Verify(h => h.Set("x-b3-spanid", "000000003ade68b1"), Times.Once());
            headers.Verify(h => h.Set("x-b3-sampled", "1"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract default (no sampler) sampling from trace context
            var newContext = new SpanContext(parent: null, new TraceContext(Mock.Of<IDatadogTracer>()), serviceName: null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();

            B3Propagator.Inject(newContext, newHeaders.Object);

            newHeaders.Verify(h => h.Set("x-b3-traceid", "0123456789abcdef1122334455667788"), Times.Once());
            newHeaders.Verify(h => h.Set("x-b3-spanid", "000000003ade68b1"), Times.Once());
            newHeaders.Verify(h => h.Set("x-b3-sampled", "1"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            // override sampling decision
            newContext.TraceContext.SetSamplingPriority(SamplingPriorityValues.UserReject);
            newHeaders = new Mock<IHeadersCollection>();

            B3Propagator.Inject(newContext, newHeaders.Object);

            newHeaders.Verify(h => h.Set("x-b3-traceid", "0123456789abcdef1122334455667788"), Times.Once());
            newHeaders.Verify(h => h.Set("x-b3-spanid", "000000003ade68b1"), Times.Once());
            newHeaders.Verify(h => h.Set("x-b3-sampled", "0"), Times.Once());
            newHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            var traceId = new TraceId(0x0123456789abcdef, 0x1122334455667788); // 0x0123456789abcdef1122334455667788
            ulong spanId = 0x000000003ade68b1;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, null);

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            B3Propagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set("x-b3-traceid", "0123456789abcdef1122334455667788"), Times.Once());
            headers.Verify(h => h.Set("x-b3-spanid", "000000003ade68b1"), Times.Once());
            headers.Verify(h => h.Set("x-b3-sampled", "1"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract default (no sampler) sampling from trace context
            var newContext = new SpanContext(parent: null, new TraceContext(Mock.Of<IDatadogTracer>()), serviceName: null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();

            B3Propagator.Inject(newContext, newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));

            newHeaders.Verify(h => h.Set("x-b3-traceid", "0123456789abcdef1122334455667788"), Times.Once());
            newHeaders.Verify(h => h.Set("x-b3-spanid", "000000003ade68b1"), Times.Once());
            newHeaders.Verify(h => h.Set("x-b3-sampled", "1"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            // override sampling decision
            newContext.TraceContext.SetSamplingPriority(SamplingPriorityValues.UserReject);
            newHeaders = new Mock<IHeadersCollection>();

            B3Propagator.Inject(newContext, newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));

            newHeaders.Verify(h => h.Set("x-b3-traceid", "0123456789abcdef1122334455667788"), Times.Once());
            newHeaders.Verify(h => h.Set("x-b3-spanid", "000000003ade68b1"), Times.Once());
            newHeaders.Verify(h => h.Set("x-b3-sampled", "0"), Times.Once());
            newHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void CreateHeaderWith64BitTraceId()
        {
            var traceId = (TraceId)0x00000000075bcd15; // 123456789
            ulong spanId = 0x000000003ade68b1;         // 987654321;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, origin: null);

            B3MultipleHeaderContextPropagator.CreateHeaders(context, out var traceIdHeader, out var spanIdHeader, out _);
            traceIdHeader.Should().Be("000000000000000000000000075bcd15");
            spanIdHeader.Should().Be("000000003ade68b1");
        }

        [Fact]
        public void CreateHeaderWith128BitTraceId()
        {
            var traceId = new TraceId(0x1234567890abcdef, 0x1122334455667788);
            ulong spanId = 0x000000003ade68b1;
            var samplingPriority = SamplingPriorityValues.AutoReject;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, origin: null);

            B3MultipleHeaderContextPropagator.CreateHeaders(context, out var traceIdHeader, out var spanIdHeader, out _);
            traceIdHeader.Should().Be("1234567890abcdef1122334455667788");
            spanIdHeader.Should().Be("000000003ade68b1");
        }

        [Theory]
        [InlineData("00000000075bcd15", "000000003ade68b1", "1", 0, 123456789, 987654321, "00000000075bcd15", "000000003ade68b1", SamplingPriorityValues.AutoKeep)]
        [InlineData("00000000075bcd15", "000000003ade68b1", "0", 0, 123456789, 987654321, "00000000075bcd15", "000000003ade68b1", SamplingPriorityValues.AutoReject)]
        [InlineData("1234567890abcdef1122334455667788", "000000003ade68b1", "1", 0x1234567890abcdef, 0x1122334455667788, 987654321, "1234567890abcdef1122334455667788", "000000003ade68b1", SamplingPriorityValues.AutoKeep)]
        public void Extract_IHeadersCollection(
            string traceIdHeader,
            string spanIdHeader,
            string sampledHeader,
            ulong traceIdUpper,
            ulong traceIdLower,
            ulong spanId,
            string rawTraceId,
            string rawSpanId,
            int samplingPriority)
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { traceIdHeader });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { spanIdHeader });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { sampledHeader });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("x-b3-traceid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-spanid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-sampled"), Times.Once());

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = new TraceId(traceIdUpper, traceIdLower),
                           TraceId = traceIdLower,
                           SpanId = spanId,
                           RawTraceId = rawTraceId,
                           RawSpanId = rawSpanId,
                           Origin = null,
                           SamplingPriority = samplingPriority,
                           IsRemote = true,
                       });
        }

        [Theory]
        [InlineData("00000000075bcd15", "000000003ade68b1", "1", 0, 123456789, 987654321, "00000000075bcd15", "000000003ade68b1", SamplingPriorityValues.AutoKeep)]
        [InlineData("00000000075bcd15", "000000003ade68b1", "0", 0, 123456789, 987654321, "00000000075bcd15", "000000003ade68b1", SamplingPriorityValues.AutoReject)]
        [InlineData("1234567890abcdef1122334455667788", "000000003ade68b1", "1", 0x1234567890abcdef, 0x1122334455667788, 987654321, "1234567890abcdef1122334455667788", "000000003ade68b1", SamplingPriorityValues.AutoKeep)]
        public void Extract_CarrierAndDelegate(
            string traceIdHeader,
            string spanIdHeader,
            string sampledHeader,
            ulong traceIdUpper,
            ulong traceIdLower,
            ulong spanId,
            string rawTraceId,
            string rawSpanId,
            int samplingPriority)
        {
            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { traceIdHeader });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { spanIdHeader });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { sampledHeader });

            var result = B3Propagator.Extract(headers.Object, (carrier, name) => carrier.GetValues(name));

            headers.Verify(h => h.GetValues("x-b3-traceid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-spanid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-sampled"), Times.Once());

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = new TraceId(traceIdUpper, traceIdLower),
                           TraceId = traceIdLower,
                           SpanId = spanId,
                           RawTraceId = rawTraceId,
                           RawSpanId = rawSpanId,
                           Origin = null,
                           SamplingPriority = samplingPriority,
                           IsRemote = true,
                       });
        }

        [Fact]
        public void ExtractAndInject_PreserveOriginalTraceId()
        {
            const string traceId = "0af7651916cd43dd8448eb211c80319c";
            const string spanId = "00f067aa0ba902b7";
            const string sampled = "1";
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { traceId });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { spanId });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { sampled });

            var expectedTraceId = new TraceId(0x0af7651916cd43dd, 0x8448eb211c80319c);
            const ulong expectedSpanId = 0x00f067aa0ba902b7UL;

            var result = B3Propagator.Extract(headers.Object);

            result.Should().NotBeNull();
            result!.TraceId128.Should().Be(expectedTraceId);
            result.TraceId.Should().Be(expectedTraceId.Lower);
            result.SpanId.Should().Be(expectedSpanId);

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headersForInjection.Setup(h => h.Set("x-b3-traceid", traceId));
            headersForInjection.Setup(h => h.Set("x-b3-spanid", spanId));
            headersForInjection.Setup(h => h.Set("x-b3-sampled", sampled));

            B3Propagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set("x-b3-traceid", traceId), Times.Once());
            headersForInjection.Verify(h => h.Set("x-b3-spanid", spanId), Times.Once());
            headersForInjection.Verify(h => h.Set("x-b3-sampled", sampled), Times.Once());
        }

        [Fact]
        public void Extract_InvalidLength()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { "4321000000000000000000000000075bcd15" }); // 36 chars
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("x-b3-traceid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-spanid"), Times.Never);  // extractor doesn't get this far
            headers.Verify(h => h.GetValues("x-b3-sampled"), Times.Never); // extractor doesn't get this far

            result.Should().BeNull();
        }

        [Fact]
        public void Extract_InvalidFormat()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { "432100000000000000000==00000075bcd15" });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { "00000+003ade68b1" });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("x-b3-traceid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-spanid"), Times.Never);  // extractor doesn't get this far
            headers.Verify(h => h.GetValues("x-b3-sampled"), Times.Never); // extractor doesn't get this far

            result.Should().BeNull();
        }

        [Fact]
        public void Extract_EmptyTraceId()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { "          " });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("x-b3-traceid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-spanid"), Times.Never);  // extractor doesn't get this far
            headers.Verify(h => h.GetValues("x-b3-sampled"), Times.Never); // extractor doesn't get this far

            result.Should().BeNull();
        }

        [Fact]
        public void Extract_EmptySpanId()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { "000000000000000000000000075bcd15" });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { "       " });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("x-b3-traceid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-spanid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-sampled"), Times.Never); // extractor doesn't get this far

            result.Should().BeNull();
        }

        [Fact]
        public void Extract_InvalidTraceId()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { "00000000000000000000000000000000" });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("x-b3-traceid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-spanid"), Times.Never());  // extractor doesn't get this far
            headers.Verify(h => h.GetValues("x-b3-sampled"), Times.Never()); // extractor doesn't get this far

            result.Should().BeNull();
        }

        [Fact]
        public void Extract_InvalidSpanIdLength()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { "000000000000000000000000075bcd15" });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { "    432   " });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { "1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("x-b3-traceid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-spanid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-sampled"), Times.Never); // extractor doesn't get this far

            result.Should().BeNull();
        }
    }
}
