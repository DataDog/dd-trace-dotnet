// <copyright file="MultiSpanContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class MultiSpanContextPropagatorTests
    {
        private const string PropagatedTagsString = "_dd.p.key1=value1,_dd.p.key2=value2";
        private const string ZeroLastParentId = "0000000000000000";
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        private static readonly TraceTagCollection PropagatedTagsCollection = new(
            new List<KeyValuePair<string, string>>
            {
                new("_dd.p.key1", "value1"),
                new("_dd.p.key2", "value2"),
            },
            PropagatedTagsString);

        private static readonly TraceTagCollection EmptyPropagatedTags = new();

        private static readonly SpanContextPropagator Propagator;

        private static readonly SpanContextPropagator W3CDatadogPropagatorExtractFirstTrue;
        private static readonly SpanContextPropagator DatadogW3CPropagatorExtractFirstTrue;
        private static readonly SpanContextPropagator W3CDatadogPropagatorExtractFirstFalse;
        private static readonly SpanContextPropagator DatadogW3CPropagatorExtractFirstFalse;
        private static readonly SpanContextPropagator DatadogB3PropagatorExtractFirstFalse;
        private static readonly SpanContextPropagator B3W3CPropagatorExtractFirstFalse;

        static MultiSpanContextPropagatorTests()
        {
            var names = new[]
                        {
                            ContextPropagationHeaderStyle.W3CTraceContext,
                            ContextPropagationHeaderStyle.Datadog,
                            ContextPropagationHeaderStyle.B3MultipleHeaders,
                            ContextPropagationHeaderStyle.B3SingleHeader,
                        };

            Propagator = SpanContextPropagatorFactory.GetSpanContextPropagator(names, names, true);

            // W3CTraceContext-Datadog Extracts first header only
            W3CDatadogPropagatorExtractFirstTrue = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[]
                {
                    ContextPropagationHeaderStyle.W3CTraceContext,
                    ContextPropagationHeaderStyle.Datadog,
                },
                new[]
                {
                    ContextPropagationHeaderStyle.W3CTraceContext,
                    ContextPropagationHeaderStyle.Datadog,
                },
                true);

            // Datadog-W3CTraceContext Extracts first header only
            DatadogW3CPropagatorExtractFirstTrue = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[]
                {
                    ContextPropagationHeaderStyle.Datadog,
                    ContextPropagationHeaderStyle.W3CTraceContext,
                },
                new[]
                {
                    ContextPropagationHeaderStyle.Datadog,
                    ContextPropagationHeaderStyle.W3CTraceContext,
                },
                true);

            // W3CTraceContext-Datadog
            W3CDatadogPropagatorExtractFirstFalse = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[]
                {
                    ContextPropagationHeaderStyle.W3CTraceContext,
                    ContextPropagationHeaderStyle.Datadog,
                },
                new[]
                {
                    ContextPropagationHeaderStyle.W3CTraceContext,
                    ContextPropagationHeaderStyle.Datadog,
                },
                false);

            // Datadog-W3CTraceContext
            DatadogW3CPropagatorExtractFirstFalse = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[]
                {
                    ContextPropagationHeaderStyle.Datadog,
                    ContextPropagationHeaderStyle.W3CTraceContext,
                },
                new[]
                {
                    ContextPropagationHeaderStyle.Datadog,
                    ContextPropagationHeaderStyle.W3CTraceContext,
                },
                false);

            // Datadog-B3MultipleHeaders
            DatadogB3PropagatorExtractFirstFalse = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[]
                {
                    ContextPropagationHeaderStyle.Datadog,
                    ContextPropagationHeaderStyle.B3MultipleHeaders,
                },
                new[]
                {
                    ContextPropagationHeaderStyle.Datadog,
                    ContextPropagationHeaderStyle.B3MultipleHeaders,
                },
                false);

            // B3MultipleHeaders-W3CTraceContext
            B3W3CPropagatorExtractFirstFalse = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[]
                {
                    ContextPropagationHeaderStyle.B3MultipleHeaders,
                    ContextPropagationHeaderStyle.W3CTraceContext,
                },
                new[]
                {
                    ContextPropagationHeaderStyle.B3MultipleHeaders,
                    ContextPropagationHeaderStyle.W3CTraceContext,
                },
                false);
        }

        [Fact]
        public void Inject_All_IHeadersCollection()
        {
            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>());
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
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:rum;p:000000003ade68b1;t.key1:value1;t.key2:value2"), Times.Once());

            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_All_IHeadersCollection_128Bit_TraceId()
        {
            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>());
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep);
            traceContext.Origin = "rum";
            traceContext.Tags.SetTags(PropagatedTagsCollection);

            var traceId = new TraceId(0x1234567890abcdef, 0x1122334455667788);
            var spanId = 1UL;

            var context = new SpanContext(
                parent: SpanContext.None,
                traceContext,
                serviceName: null,
                traceId: traceId,
                spanId: spanId,
                rawTraceId: traceId.ToString(),
                rawSpanId: spanId.ToString("x16"));

            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            headers.Verify(h => h.Set("x-datadog-trace-id", traceId.Lower.ToString(InvariantCulture)), Times.Once());
            headers.Verify(h => h.Set("x-datadog-parent-id", spanId.ToString(InvariantCulture)), Times.Once());
            headers.Verify(h => h.Set("x-datadog-sampling-priority", "2"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-origin", "rum"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-tags", PropagatedTagsString + ",_dd.p.tid=1234567890abcdef"), Times.Once());

            headers.Verify(h => h.Set("x-b3-traceid", "1234567890abcdef1122334455667788"), Times.Once());
            headers.Verify(h => h.Set("x-b3-spanid", "0000000000000001"), Times.Once());
            headers.Verify(h => h.Set("x-b3-sampled", "1"), Times.Once());

            headers.Verify(h => h.Set("b3", "1234567890abcdef1122334455667788-0000000000000001-1"), Times.Once());

            headers.Verify(h => h.Set("traceparent", "00-1234567890abcdef1122334455667788-0000000000000001-01"), Times.Once());
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:rum;p:0000000000000001;t.key1:value1;t.key2:value2"), Times.Once());

            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_All_CarrierAndDelegate()
        {
            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>());
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
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:rum;p:000000003ade68b1;t.key1:value1;t.key2:value2"), Times.Once());

            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_All_CarrierAndDelegate_128Bit_TraceId()
        {
            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>());
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep);
            traceContext.Origin = "rum";
            traceContext.Tags.SetTags(PropagatedTagsCollection);

            var traceId = new TraceId(0x1234567890abcdef, 0x1122334455667788);
            var spanId = 1UL;

            var context = new SpanContext(
                parent: SpanContext.None,
                traceContext,
                serviceName: null,
                traceId: traceId,
                spanId: spanId,
                rawTraceId: traceId.ToString(),
                rawSpanId: spanId.ToString("x16"));

            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set("x-datadog-trace-id", traceId.Lower.ToString(InvariantCulture)), Times.Once());
            headers.Verify(h => h.Set("x-datadog-parent-id", spanId.ToString(InvariantCulture)), Times.Once());
            headers.Verify(h => h.Set("x-datadog-sampling-priority", "2"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-origin", "rum"), Times.Once());
            headers.Verify(h => h.Set("x-datadog-tags", PropagatedTagsString + ",_dd.p.tid=1234567890abcdef"), Times.Once());

            headers.Verify(h => h.Set("x-b3-traceid", "1234567890abcdef1122334455667788"), Times.Once());
            headers.Verify(h => h.Set("x-b3-spanid", "0000000000000001"), Times.Once());
            headers.Verify(h => h.Set("x-b3-sampled", "1"), Times.Once());

            headers.Verify(h => h.Set("b3", "1234567890abcdef1122334455667788-0000000000000001-1"), Times.Once());

            headers.Verify(h => h.Set("traceparent", "00-1234567890abcdef1122334455667788-0000000000000001-01"), Times.Once());
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:rum;p:0000000000000001;t.key1:value1;t.key2:value2"), Times.Once());

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
                           IsRemote = true,
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
                           IsRemote = true,
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
                           IsRemote = true,
                           LastParentId = ZeroLastParentId,
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
                           IsRemote = true,
                           LastParentId = ZeroLastParentId,
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
                           IsRemote = true,
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

        // Tests for making sure the behaviour of either copying the valid tracecontext or not is accurate
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TraceContextPrecedence_Respected_WhenHavingMatchingTraceIds(bool extractFirst, bool w3CHeaderFirst)
        {
            // headers1 equivalent from system-tests
            var headers = new Mock<IHeadersCollection>();

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-11111111111111110000000000000001-000000003ade68b1-01" });
            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:2;o:rum;p:0123456789abcdef;t.tid:1111111111111111,foo=1" });
            headers.Setup(h => h.GetValues("x-datadog-trace-id"))
                   .Returns(new[] { "1" });
            headers.Setup(h => h.GetValues("x-datadog-parent-id"))
                   .Returns(new[] { "987654321" });
            headers.Setup(h => h.GetValues("x-datadog-sampling-priority"))
                   .Returns(new[] { "2" });
            headers.Setup(h => h.GetValues("x-datadog-origin"))
                   .Returns(new[] { "rum" });
            headers.Setup(h => h.GetValues("x-datadog-tags"))
                   .Returns(new[] { "_dd.p.tid=1111111111111111" });

            var result = GetPropagatorToTest(extractFirst, w3CHeaderFirst).Extract(headers.Object);

            TraceTagCollection propagatedTags = new(
                new List<KeyValuePair<string, string>>
                {
                    new("_dd.p.tid", "1111111111111111"),
                },
                null);

            result.Should()
                  .NotBeNull()
                  .And
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = new TraceId(0x1111111111111111, 1),
                           TraceId = 1,
                           SpanId = 987654321,
                           RawTraceId = "11111111111111110000000000000001",
                           RawSpanId = "000000003ade68b1",
                           SamplingPriority = SamplingPriorityValues.UserKeep,
                           PropagatedTags = propagatedTags,
                           Origin = "rum",
                           AdditionalW3CTraceState = !extractFirst || w3CHeaderFirst ? "foo=1" : null,
                           Parent = null,
                           ParentId = null,
                           IsRemote = true,
                           LastParentId = w3CHeaderFirst ? "0123456789abcdef" : null, // if we have Datadog headers don't use p
                       });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TraceContextPrecedence_Correct_WithDifferentTracestate(bool extractFirst, bool w3CHeaderFirst)
        {
            // headers2 equivalent from system-tests
            var headers = new Mock<IHeadersCollection>();

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-11111111111111110000000000000002-000000003ade68b1-01" });
            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:1;p:0123456789abcdef;t.tid:1111111111111111,foo=1" });
            headers.Setup(h => h.GetValues("x-datadog-trace-id"))
                   .Returns(new[] { "2" });
            headers.Setup(h => h.GetValues("x-datadog-parent-id"))
                   .Returns(new[] { "987654321" });
            headers.Setup(h => h.GetValues("x-datadog-sampling-priority"))
                   .Returns(new[] { "2" });
            headers.Setup(h => h.GetValues("x-datadog-tags"))
                   .Returns(new[] { "_dd.p.tid=1111111111111111" });

            var result = GetPropagatorToTest(extractFirst, w3CHeaderFirst).Extract(headers.Object);

            TraceTagCollection propagatedTags = new(
                new List<KeyValuePair<string, string>>
                {
                    new("_dd.p.tid", "1111111111111111"),
                },
                null);

            result.Should()
                  .NotBeNull()
                  .And
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = new TraceId(0x1111111111111111, 2),
                           TraceId = 2,
                           SpanId = 987654321,
                           RawTraceId = "11111111111111110000000000000002",
                           RawSpanId = "000000003ade68b1",
                           SamplingPriority = w3CHeaderFirst ? 1 : 2,
                           PropagatedTags = propagatedTags,
                           AdditionalW3CTraceState = !extractFirst || w3CHeaderFirst ? "foo=1" : null,
                           Parent = null,
                           ParentId = null,
                           IsRemote = true,
                           LastParentId = w3CHeaderFirst ? "0123456789abcdef" : null, // if we have Datadog headers don't use p
                       });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TraceContextPrecedence_ExtractedState_WhenMissingDD_OnTracestate(bool extractFirst, bool w3CHeaderFirst)
        {
            // headers3 equivalent from system-tests
            var headers = new Mock<IHeadersCollection>();

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-11111111111111110000000000000003-000000003ade68b1-01" });
            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "foo=1" });
            headers.Setup(h => h.GetValues("x-datadog-trace-id"))
                   .Returns(new[] { "3" });
            headers.Setup(h => h.GetValues("x-datadog-parent-id"))
                   .Returns(new[] { "987654321" });
            headers.Setup(h => h.GetValues("x-datadog-sampling-priority"))
                   .Returns(new[] { "2" });
            headers.Setup(h => h.GetValues("x-datadog-tags"))
                   .Returns(new[] { "_dd.p.tid=1111111111111111" });

            var result = GetPropagatorToTest(extractFirst, w3CHeaderFirst).Extract(headers.Object);

            TraceTagCollection propagatedTags = new(
                new List<KeyValuePair<string, string>>
                {
                    new("_dd.p.tid", "1111111111111111"),
                },
                null);

            result.Should()
                  .NotBeNull()
                  .And
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = new TraceId(0x1111111111111111, 3),
                           TraceId = 3,
                           SpanId = 987654321,
                           RawTraceId = "11111111111111110000000000000003",
                           RawSpanId = "000000003ade68b1",
                           SamplingPriority = w3CHeaderFirst ? 1 : 2,
                           PropagatedTags = !w3CHeaderFirst ? propagatedTags : new TraceTagCollection(),
                           AdditionalW3CTraceState = !extractFirst || w3CHeaderFirst ? "foo=1" : null,
                           Parent = null,
                           ParentId = null,
                           IsRemote = true,
                           LastParentId = w3CHeaderFirst ? ZeroLastParentId : null,
                       });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TraceContextPrecedence_ConsistentBehaviour_WithDifferentParentId(bool extractFirst, bool w3CHeaderFirst)
        {
            // headers4 equivalent from system-tests
            var headers = new Mock<IHeadersCollection>();

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-11111111111111110000000000000004-000000003ade68b1-01" });
            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:2;p:0123456789abcdef;t.tid:1111111111111111,foo=1" });
            headers.Setup(h => h.GetValues("x-datadog-trace-id"))
                   .Returns(new[] { "4" });
            headers.Setup(h => h.GetValues("x-datadog-parent-id"))
                   .Returns(new[] { "3540" });
            headers.Setup(h => h.GetValues("x-datadog-sampling-priority"))
                   .Returns(new[] { "2" });
            headers.Setup(h => h.GetValues("x-datadog-tags"))
                   .Returns(new[] { "_dd.p.tid=1111111111111111" });

            var result = GetPropagatorToTest(extractFirst, w3CHeaderFirst).Extract(headers.Object);

            TraceTagCollection propagatedTags = new(
                new List<KeyValuePair<string, string>>
                {
                    new("_dd.p.tid", "1111111111111111"),
                },
                null);

            bool expectW3cParentIds = w3CHeaderFirst || !extractFirst;

            result.Should()
                  .NotBeNull()
                  .And
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = new TraceId(0x1111111111111111, 4),
                           TraceId = 4,
                           SpanId = (ulong)(expectW3cParentIds ? 987654321 : 3540),
                           RawTraceId = "11111111111111110000000000000004",
                           RawSpanId = expectW3cParentIds ? "000000003ade68b1" : "0000000000000dd4",
                           SamplingPriority = 2,
                           PropagatedTags = propagatedTags,
                           AdditionalW3CTraceState = expectW3cParentIds ? "foo=1" : null,
                           Parent = null,
                           ParentId = null,
                           IsRemote = true,
                           LastParentId =  expectW3cParentIds ? "0123456789abcdef" : null,
                       });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TraceContextPrecedence_ConsistentBehaviour_WithDifferentTraceIds(bool extractFirst, bool w3CHeaderFirst)
        {
            // headers5 equivalent from system-tests
            var headers = new Mock<IHeadersCollection>();

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-11111111111111110000000000000005-000000003ade68b1-01" });
            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "foo=1" });
            headers.Setup(h => h.GetValues("x-datadog-trace-id"))
                   .Returns(new[] { "3541" });
            headers.Setup(h => h.GetValues("x-datadog-parent-id"))
                   .Returns(new[] { "987654321" });
            headers.Setup(h => h.GetValues("x-datadog-sampling-priority"))
                   .Returns(new[] { "2" });
            headers.Setup(h => h.GetValues("x-datadog-tags"))
                   .Returns(new[] { "_dd.p.tid=1111111111111111" });

            var result = GetPropagatorToTest(extractFirst, w3CHeaderFirst).Extract(headers.Object);

            TraceTagCollection propagatedTags = new(
                new List<KeyValuePair<string, string>>
                {
                    new("_dd.p.tid", "1111111111111111"),
                },
                null);

            var traceId = new TraceId(0x1111111111111111, (ulong)(w3CHeaderFirst ? 5 : 0xdd5));

            result.Should()
                  .NotBeNull()
                  .And
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = traceId,
                           TraceId = (ulong)(w3CHeaderFirst ? 5 : 0xdd5),
                           SpanId = 987654321,
                           RawTraceId = traceId.ToString(),
                           RawSpanId = "000000003ade68b1",
                           SamplingPriority = w3CHeaderFirst ? 1 : 2,
                           PropagatedTags = !w3CHeaderFirst ? propagatedTags : new TraceTagCollection(),
                           AdditionalW3CTraceState = w3CHeaderFirst ? "foo=1" : null,
                           Parent = null,
                           ParentId = null,
                           IsRemote = true,
                           LastParentId = w3CHeaderFirst ? ZeroLastParentId : null,
                       });
        }

        [Theory]
        [InlineData("dd=s:2;p:000000003ade68b1,foo=1", "1", "987654321", 987654321, null)]
        [InlineData("dd=s:2;p:000000000000000a,foo=1", "2", "10", 10, null)]
        [InlineData("dd=s:2;p:000000000000000a,foo=1", "1", "10", 987654321, "000000000000000a")]
        [InlineData("dd=s:2,foo=1", "1", "10", 987654321, "000000000000000a")]
        [InlineData("dd=s:2;p:8fffffffffffffff,foo=1", "1", "10", 987654321, "8fffffffffffffff")]
        public void Datadog_W3C_TraceState_ParentExtracted(string tracestate, string traceId, string parentId, ulong expectedSpanId, string expectedParentTag)
        {
            var uLongParentId = Convert.ToUInt64(parentId);
            var headers = new NameValueHeadersCollection(new NameValueCollection());

            // W3C Headers
            headers.Add("traceparent", "00-00000000000000000000000000000001-000000003ade68b1-01");
            headers.Add("tracestate", tracestate);
            // Datadog Headers
            headers.Add("x-datadog-trace-id", traceId);
            headers.Add("x-datadog-parent-id", parentId);
            // B3 Multi Headers
            headers.Add("x-b3-traceid", $"0000000000000000000000000000000{traceId}");
            headers.Add("x-b3-spanid", $"{HexString.ToHexString(uLongParentId)}");
            headers.Add("x-b3-sampled", "1");

            var resultDatadogW3C = DatadogW3CPropagatorExtractFirstFalse.Extract(headers);
            resultDatadogW3C.Should().NotBeNull();
            resultDatadogW3C?.SpanId.Should().Be(expectedSpanId);
            resultDatadogW3C?.LastParentId.Should().Be(expectedParentTag);

            var resultDatadogB3 = DatadogB3PropagatorExtractFirstFalse.Extract(headers);
            resultDatadogB3.Should().NotBeNull();
            resultDatadogB3?.SpanId.Should().Be(uLongParentId);
            resultDatadogB3?.LastParentId.Should().Be(null);

            var resultB3W3C = B3W3CPropagatorExtractFirstFalse.Extract(headers);
            resultB3W3C.Should().NotBeNull();
            resultB3W3C?.SpanId.Should().Be(expectedSpanId);
            resultB3W3C?.LastParentId.Should().Be(expectedParentTag);
        }

        private SpanContextPropagator GetPropagatorToTest(bool extractFirst, bool w3CHeaderFirst)
            => (w3CHeaderFirst, extractFirst) switch
        {
            (true, true) => W3CDatadogPropagatorExtractFirstTrue,
            (true, false) => W3CDatadogPropagatorExtractFirstFalse,
            (false, true) => DatadogW3CPropagatorExtractFirstTrue,
            (false, false) => DatadogW3CPropagatorExtractFirstFalse
        };
    }
}
