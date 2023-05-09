// <copyright file="MultiSpanContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class MultiSpanContextPropagatorTests
    {
        private const string PropagatedTagsString = "_dd.p.key1=value1,_dd.p.key2=value2";

        private static readonly TraceTagCollection PropagatedTagsCollection = new(
            new List<KeyValuePair<string, string>>
            {
                new("_dd.p.key1", "value1"),
                new("_dd.p.key2", "value2"),
            },
            PropagatedTagsString);

        private static readonly TraceTagCollection EmptyPropagatedTags = new();

        private static readonly SpanContextPropagator Propagator;

        static MultiSpanContextPropagatorTests()
        {
            var names = new[]
                        {
                            ContextPropagationHeaderStyle.W3CTraceContext,
                            ContextPropagationHeaderStyle.Datadog,
                            ContextPropagationHeaderStyle.B3MultipleHeaders,
                            ContextPropagationHeaderStyle.B3SingleHeader,
                        };

            Propagator = SpanContextPropagatorFactory.GetSpanContextPropagator(names, names);
        }

        [Fact]
        public void Inject_All_IHeadersCollection()
        {
            var traceContext = new TraceContext(tracer: null);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep);
            traceContext.Origin = "rum";
            traceContext.Tags.SetTags(PropagatedTagsCollection);

            var context = new SpanContext(
                parent: SpanContext.None,
                traceContext,
                serviceName: null,
                (TraceId)123456789,
                spanId: 987654321);

            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            headers.Verify(h => h.Set("x-datadog-trace-id", "123456789"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-parent-id", "987654321"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-sampling-priority", "2"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-origin", "rum"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-tags", PropagatedTagsString), Times.Once());

            headers.Verify(h => h.Set("x-b3-traceid", "000000000000000000000000075bcd15"), Times.Once());
            headers.Verify(h => h.Set("x-b3-spanid", "000000003ade68b1"), Times.Once());
            headers.Verify(h => h.Set("x-b3-sampled", "1"), Times.Once());

            headers.Verify(h => h.Set("b3", "000000000000000000000000075bcd15-000000003ade68b1-1"), Times.Once());

            headers.Verify(h => h.Set("traceparent", "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:rum;t.key1:value1;t.key2:value2"), Times.Once());

            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_All_CarrierAndDelegate()
        {
            var traceContext = new TraceContext(tracer: null);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep);
            traceContext.Origin = "rum";
            traceContext.Tags.SetTags(PropagatedTagsCollection);

            var context = new SpanContext(
                parent: SpanContext.None,
                traceContext,
                serviceName: null,
                (TraceId)123456789,
                987654321);

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set("x-datadog-trace-id", "123456789"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-parent-id", "987654321"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-sampling-priority", "2"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-origin", "rum"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-tags", PropagatedTagsString), Times.Once());

            headers.Verify(h => h.Set("x-b3-traceid", "000000000000000000000000075bcd15"), Times.Once());
            headers.Verify(h => h.Set("x-b3-spanid", "000000003ade68b1"), Times.Once());
            headers.Verify(h => h.Set("x-b3-sampled", "1"), Times.Once());

            headers.Verify(h => h.Set("b3", "000000000000000000000000075bcd15-000000003ade68b1-1"), Times.Once());

            headers.Verify(h => h.Set("traceparent", "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:rum;t.key1:value1;t.key2:value2"), Times.Once());

            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Extract_B3_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>();

            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { "000000000000000000000000075bcd15" });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { "000000003ade68b1" });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { "1" });

            var result = Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("x-b3-traceid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-spanid"), Times.Once());
            headers.Verify(h => h.GetValues("x-b3-sampled"), Times.Once());

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = (TraceId)123456789,
                           TraceId = 123456789,
                           SpanId = 987654321,
                           RawTraceId = "000000000000000000000000075bcd15",
                           RawSpanId = "000000003ade68b1",
                           Origin = null,
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                       });
        }

        [Fact]
        public void Extract_B3SingleHeader_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>();

            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { "00000000075bcd15-000000003ade68b1-1" });

            var result = Propagator.Extract(headers.Object);

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
        public void Extract_W3C_IHeadersCollection_traceparent()
        {
            var headers = new Mock<IHeadersCollection>();

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            var result = Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("traceparent"), Times.Once());
            headers.Verify(h => h.GetValues("tracestate"), Times.Once());

            result.Should()
                  .NotBeNull()
                  .And
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = (TraceId)123456789,
                           TraceId = 123456789,
                           SpanId = 987654321,
                           RawTraceId = "000000000000000000000000075bcd15",
                           RawSpanId = "000000003ade68b1",
                           Origin = null,
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                           PropagatedTags = EmptyPropagatedTags,
                       });
        }

        [Fact]
        public void Extract_W3C_IHeadersCollection_traceparent_tracestate()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:2;o:rum;t.key1:value1;t.key2:value2" });

            var result = Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("traceparent"), Times.Once());
            headers.Verify(h => h.GetValues("tracestate"), Times.Once());
            headers.VerifyNoOtherCalls();

            result.Should()
                  .NotBeNull()
                  .And
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = (TraceId)123456789,
                           TraceId = 123456789,
                           SpanId = 987654321,
                           RawTraceId = "000000000000000000000000075bcd15",
                           RawSpanId = "000000003ade68b1",
                           SamplingPriority = SamplingPriorityValues.UserKeep,
                           Origin = "rum",
                           PropagatedTags = PropagatedTagsCollection,
                           Parent = null,
                           ParentId = null,
                       });
        }

        [Fact]
        public void Extract_Datadog_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>();

            headers.Setup(h => h.GetValues("x-datadog-trace-id"))
                   .Returns(new[] { "123456789" });
            headers.Setup(h => h.GetValues("x-datadog-parent-id"))
                   .Returns(new[] { "987654321" });
            headers.Setup(h => h.GetValues("x-datadog-sampling-priority"))
                   .Returns(new[] { "1" });
            headers.Setup(h => h.GetValues("x-datadog-origin"))
                   .Returns(new[] { "rum" });
            headers.Setup(h => h.GetValues("x-datadog-tags"))
                   .Returns(new[] { "_dd.p.key1=value1,_dd.p.key2=value2" });

            var result = Propagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("x-datadog-trace-id"), Times.Once());
            headers.Verify(h => h.GetValues("x-datadog-parent-id"), Times.Once());
            headers.Verify(h => h.GetValues("x-datadog-sampling-priority"), Times.Once());
            headers.Verify(h => h.GetValues("x-datadog-origin"), Times.Once());
            headers.Verify(h => h.GetValues("x-datadog-tags"), Times.Once());

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = (TraceId)123456789,
                           TraceId = 123456789,
                           RawTraceId = "000000000000000000000000075bcd15",
                           SpanId = 987654321,
                           RawSpanId = "000000003ade68b1",
                           Origin = "rum",
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                           PropagatedTags = PropagatedTagsCollection,
                       });
        }

        [Fact]
        public void ExtractAndInject_W3C_PreserveOriginalTraceId()
        {
            const string traceId = "0af7651916cd43dd8448eb211c80319c";
            const string spanId = "00f067aa0ba902b7";
            const string expectedTraceParent = $"00-{traceId}-{spanId}-01";
            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { expectedTraceParent });

            var expectedTraceId = new TraceId(0x0af7651916cd43dd, 0x8448eb211c80319c);
            const ulong expectedSpanId = 0x00f067aa0ba902b7UL;

            var result = Propagator.Extract(headers.Object);

            result.Should().NotBeNull();
            result!.TraceId128.Should().Be(expectedTraceId);
            result.TraceId.Should().Be(expectedTraceId.Lower);
            result.SpanId.Should().Be(expectedSpanId);

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>();
            headersForInjection.Setup(h => h.Set("traceparent", expectedTraceParent));

            Propagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set("traceparent", expectedTraceParent), Times.Once());
        }

        [Fact]
        public void ExtractAndInject_B3_PreserveOriginalTraceId()
        {
            const string traceId = "0af7651916cd43dd8448eb211c80319c";
            const string spanId = "00f067aa0ba902b7";

            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues("x-b3-traceid"))
                   .Returns(new[] { traceId });
            headers.Setup(h => h.GetValues("x-b3-spanid"))
                   .Returns(new[] { spanId });
            headers.Setup(h => h.GetValues("x-b3-sampled"))
                   .Returns(new[] { "1" });

            var expectedTraceId = new TraceId(0x0af7651916cd43dd, 0x8448eb211c80319c);
            const ulong expectedSpanId = 0x00f067aa0ba902b7UL;

            var result = Propagator.Extract(headers.Object);

            result.Should().NotBeNull();
            result!.TraceId128.Should().Be(expectedTraceId);
            result.TraceId.Should().Be(expectedTraceId.Lower);
            result.SpanId.Should().Be(expectedSpanId);

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>();
            headersForInjection.Setup(h => h.Set("x-b3-traceid", traceId));
            headersForInjection.Setup(h => h.Set("x-b3-spanid", spanId));
            headersForInjection.Setup(h => h.Set("x-b3-sampled", "1"));

            Propagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set("x-b3-traceid", traceId), Times.Once());
            headersForInjection.Verify(h => h.Set("x-b3-spanid", spanId), Times.Once());
            headersForInjection.Verify(h => h.Set("x-b3-sampled", "1"), Times.Once());
        }

        [Fact]
        public void ExtractAndInject_B3SingleHeader_PreserveOriginalTraceId()
        {
            const string traceId = "0af7651916cd43dd8448eb211c80319c";
            const string spanId = "00f067aa0ba902b7";
            const string expectedTraceParent = $"{traceId}-{spanId}-1";
            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues("b3"))
                   .Returns(new[] { expectedTraceParent });

            var result = Propagator.Extract(headers.Object);

            var expectedTraceId = new TraceId(0x0af7651916cd43ddUL, 0x8448eb211c80319cUL);
            const ulong expectedSpanId = 0x00f067aa0ba902b7UL;
            Assert.Equal(expectedTraceId, result!.TraceId128);
            Assert.Equal(expectedSpanId, result.SpanId);

            // Check the injection restoring the 128 bits traceId.
            var headersForInjection = new Mock<IHeadersCollection>();
            headersForInjection.Setup(h => h.Set("b3", expectedTraceParent));

            Propagator.Inject(result, headersForInjection.Object);

            headersForInjection.Verify(h => h.Set("b3", expectedTraceParent), Times.Once());
        }
    }
}
