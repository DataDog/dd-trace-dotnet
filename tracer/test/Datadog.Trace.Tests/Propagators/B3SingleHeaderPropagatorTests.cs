// <copyright file="B3SingleHeaderPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class B3SingleHeaderPropagatorTests
    {
        private static readonly SpanContextPropagator B3Propagator;

        private static readonly Baggage TestBaggage;

        static B3SingleHeaderPropagatorTests()
        {
            B3Propagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
                [ContextPropagationHeaderStyle.B3SingleHeader],
                [ContextPropagationHeaderStyle.B3SingleHeader],
                propagationExtractFirst: false);

            TestBaggage = new Baggage
            {
                { "key1", "value1" },
                { "key2", "value2" },
            };
        }

        [Fact]
        public void Inject_IHeadersCollection()
        {
            var traceId = new TraceId(0x0123456789abcdef, 0x1122334455667788);
            const ulong spanId = 987654321;
            var spanContext1 = new SpanContext(traceId, spanId, SamplingPriorityValues.UserKeep, serviceName: null, origin: null);
            var headers = new Mock<IHeadersCollection>();

            B3Propagator.Inject(new PropagationContext(spanContext1, TestBaggage), headers.Object);

            headers.Verify(h => h.Set("b3", "0123456789abcdef1122334455667788-000000003ade68b1-1"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract default (no sampler) sampling from trace context
            var spanContext2 = new SpanContext(parent: null, new TraceContext(new StubDatadogTracer()), serviceName: null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(new PropagationContext(spanContext2, TestBaggage), newHeaders.Object);
            newHeaders.Verify(h => h.Set("b3", "0123456789abcdef1122334455667788-000000003ade68b1-1"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            // override sampling decision
            spanContext2.TraceContext.SetSamplingPriority(SamplingPriorityValues.UserReject);
            newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(new PropagationContext(spanContext2, TestBaggage), newHeaders.Object);
            newHeaders.Verify(h => h.Set("b3", "0123456789abcdef1122334455667788-000000003ade68b1-0"), Times.Once());
            newHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            var traceId = (TraceId)0x00000000075bcd15; // 123456789
            ulong spanId = 0x000000003ade68b1; // 987654321;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var spanContext1 = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, origin: null);

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            B3Propagator.Inject(new PropagationContext(spanContext1, TestBaggage), headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set("b3", "000000000000000000000000075bcd15-000000003ade68b1-1"), Times.Once());
            headers.VerifyNoOtherCalls();

            // Extract default (no sampler) sampling from trace context
            var spanContext2 = new SpanContext(parent: null, new TraceContext(new StubDatadogTracer()), serviceName: null, traceId, spanId);
            var newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(new PropagationContext(spanContext2, TestBaggage), newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));
            newHeaders.Verify(h => h.Set("b3", "000000000000000000000000075bcd15-000000003ade68b1-1"), Times.Once());
            newHeaders.VerifyNoOtherCalls();

            // override sampling decision
            spanContext2.TraceContext.SetSamplingPriority(SamplingPriorityValues.UserReject);
            newHeaders = new Mock<IHeadersCollection>();
            B3Propagator.Inject(new PropagationContext(spanContext2, TestBaggage), newHeaders.Object, (carrier, name, value) => carrier.Set(name, value));
            newHeaders.Verify(h => h.Set("b3", "000000000000000000000000075bcd15-000000003ade68b1-0"), Times.Once());
            newHeaders.VerifyNoOtherCalls();
        }

        [Fact]
        public void CreateHeaderWith64BitTraceId()
        {
            var traceId = (TraceId)0x00000000075bcd15; // 123456789
            ulong spanId = 0x000000003ade68b1;         // 987654321;
            var samplingPriority = SamplingPriorityValues.UserKeep;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, origin: null);

            B3SingleHeaderContextPropagator.CreateHeader(context)
                                           .Should()
                                           .Be("000000000000000000000000075bcd15-000000003ade68b1-1");
        }

        [Fact]
        public void CreateHeaderWith128BitTraceId()
        {
            var traceId = new TraceId(0x1234567890abcdef, 0x1122334455667788);
            ulong spanId = 0x000000003ade68b1;
            var samplingPriority = SamplingPriorityValues.AutoReject;
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, origin: null);

            B3SingleHeaderContextPropagator.CreateHeader(context)
                                           .Should()
                                           .Be("1234567890abcdef1122334455667788-000000003ade68b1-0");
        }

        [Theory]
        [InlineData("00000000075bcd15-000000003ade68b1-1", 0, 123456789, 987654321, "00000000075bcd15", "000000003ade68b1", SamplingPriorityValues.AutoKeep)]
        [InlineData("00000000075bcd15-000000003ade68b1-0", 0, 123456789, 987654321, "00000000075bcd15", "000000003ade68b1", SamplingPriorityValues.AutoReject)]
        [InlineData("1234567890abcdef1122334455667788-000000003ade68b1-1", 0x1234567890abcdef, 0x1122334455667788, 987654321, "1234567890abcdef1122334455667788", "000000003ade68b1", SamplingPriorityValues.AutoKeep)]
        public void Extract_IHeadersCollection(string header, ulong traceIdUpper, ulong traceIdLower, ulong spanId, string rawTraceId, string rawSpanId, int samplingPriority)
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { header });

            var result = B3Propagator.Extract(headers.Object);
            headers.Verify(h => h.GetValues("b3"), Times.Once());

            result.SpanContext
                  .Should()
                  .NotBeNull()
                  .And
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
                      },
                      opts => opts.ExcludingMissingMembers());

            result.Baggage.Should().BeNull();
        }

        [Theory]
        [InlineData("00000000075bcd15-000000003ade68b1-1", 0, 123456789, 987654321, "00000000075bcd15", "000000003ade68b1", SamplingPriorityValues.AutoKeep)]
        [InlineData("00000000075bcd15-000000003ade68b1-0", 0, 123456789, 987654321, "00000000075bcd15", "000000003ade68b1", SamplingPriorityValues.AutoReject)]
        [InlineData("1234567890abcdef1122334455667788-000000003ade68b1-1", 0x1234567890abcdef, 0x1122334455667788, 987654321, "1234567890abcdef1122334455667788", "000000003ade68b1", SamplingPriorityValues.AutoKeep)]
        public void Extract_CarrierAndDelegate(string header, ulong traceIdUpper, ulong traceIdLower, ulong spanId, string rawTraceId, string rawSpanId, int samplingPriority)
        {
            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { header });

            var result = B3Propagator.Extract(headers.Object, (carrier, name) => carrier.GetValues(name));

            headers.Verify(h => h.GetValues("b3"), Times.Once());

            result.SpanContext
                  .Should()
                  .NotBeNull()
                  .And
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
                      },
                      opts => opts.ExcludingMissingMembers());

            result.Baggage.Should().BeNull();
        }

        [Fact]
        public void ExtractAndInject_PreserveOriginalTraceId()
        {
            var traceId = new TraceId(0x0af7651916cd43dd, 0x8448eb211c80319c);
            var expectedTraceParent = "0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-1";
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { expectedTraceParent });

            var result = B3Propagator.Extract(headers.Object);
            var spanContext = result.SpanContext!;

            var expectedSpanId = 0x00f067aa0ba902b7UL;

            spanContext.Should().NotBeNull();
            spanContext.TraceId128.Should().Be(traceId);
            spanContext.TraceId.Should().Be(traceId.Lower);
            spanContext.SpanId.Should().Be(expectedSpanId);

            result.Baggage.Should().BeNull();

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>();
            headersForInjection.Setup(h => h.Set("b3", expectedTraceParent));

            B3Propagator.Inject(new PropagationContext(spanContext, TestBaggage), headersForInjection.Object);

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
            result.SpanContext.Should().BeNull();
            result.Baggage.Should().BeNull();
        }

        [Fact]
        public void Extract_InvalidFormat()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { "00000000075bcd15=000000003ade68b1=1" });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("b3"), Times.Once());
            result.SpanContext.Should().BeNull();
            result.Baggage.Should().BeNull();
        }

        [Fact]
        public void Extract_EmptyStrings()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { "                                   " });

            var result = B3Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("b3"), Times.Once());
            result.SpanContext.Should().BeNull();
            result.Baggage.Should().BeNull();
        }
    }
}
