// <copyright file="W3CTraceContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class W3CTraceContextPropagatorTests
    {
        private const string ZeroLastParentId = "0000000000000000";

        private static readonly TraceTagCollection PropagatedTagsCollection = new(
            new List<KeyValuePair<string, string>>
            {
                new("_dd.p.dm", "-4"),
                new("_dd.p.usr.id", "12345"),
            },
            cachedPropagationHeader: null);

        private static readonly TraceTagCollection EmptyPropagatedTags = new();

        private static readonly SpanContextPropagator W3CPropagator;

        static W3CTraceContextPropagatorTests()
        {
            W3CPropagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[] { ContextPropagationHeaderStyle.W3CTraceContext },
                new[] { ContextPropagationHeaderStyle.W3CTraceContext },
                false);
        }

        [Theory]
        [InlineData(0, 123456789, 987654321, null, "00-000000000000000000000000075bcd15-000000003ade68b1-01")]
        [InlineData(0, 123456789, 987654321, SamplingPriorityValues.UserReject, "00-000000000000000000000000075bcd15-000000003ade68b1-00")]
        [InlineData(0, 123456789, 987654321, SamplingPriorityValues.AutoReject, "00-000000000000000000000000075bcd15-000000003ade68b1-00")]
        [InlineData(0, 123456789, 987654321, SamplingPriorityValues.AutoKeep, "00-000000000000000000000000075bcd15-000000003ade68b1-01")]
        [InlineData(0, 123456789, 987654321, SamplingPriorityValues.UserKeep, "00-000000000000000000000000075bcd15-000000003ade68b1-01")]
        [InlineData(0x0123456789ABCDEF, 0x1122334455667788, 0x000000003ade68b1, null, @"00-0123456789abcdef1122334455667788-000000003ade68b1-01")]
        public void CreateTraceParentHeader(ulong traceIdUpper, ulong traceIdLower, ulong spanId, int? samplingPriority, string expected)
        {
            var traceId = new TraceId(traceIdUpper, traceIdLower);
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, "origin");
            var traceparent = W3CTraceContextPropagator.CreateTraceParentHeader(context);

            traceparent.Should().Be(expected);
        }

        [Theory]
        // null/empty/whitespace (sampling priority default is 1)
        [InlineData(null, null, null, null, "dd=s:1;p:0000000000000002")]
        [InlineData(null, "", "", "", "dd=s:1;p:0000000000000002")]
        [InlineData(null, " ", " ", " ", "dd=s:1;p:0000000000000002")]
        // sampling priority only
        [InlineData(SamplingPriorityValues.UserReject, null, null, null, "dd=s:-1;p:0000000000000002")]
        [InlineData(SamplingPriorityValues.AutoReject, null, null, null, "dd=s:0;p:0000000000000002")]
        [InlineData(SamplingPriorityValues.AutoKeep, null, null, null, "dd=s:1;p:0000000000000002")]
        [InlineData(SamplingPriorityValues.UserKeep, null, null, null, "dd=s:2;p:0000000000000002")]
        [InlineData(3, null, null, null, "dd=s:3;p:0000000000000002")]
        [InlineData(-5, null, null, null, "dd=s:-5;p:0000000000000002")]
        // origin only
        [InlineData(null, "abc", null, null, "dd=s:1;o:abc;p:0000000000000002")]
        [InlineData(null, "synthetics~;,=web", null, null, "dd=s:1;o:synthetics___~web;p:0000000000000002")]
        // propagated tags only
        [InlineData(null, null, "_dd.p.a=1", null, "dd=s:1;p:0000000000000002;t.a:1")]
        [InlineData(null, null, "_dd.p.a=1,_dd.p.b=2", null, "dd=s:1;p:0000000000000002;t.a:1;t.b:2")]
        [InlineData(null, null, "_dd.p.a=1,b=2", null, "dd=s:1;p:0000000000000002;t.a:1")]
        [InlineData(null, null, "_dd.p.usr.id=MTIzNDU=", null, "dd=s:1;p:0000000000000002;t.usr.id:MTIzNDU~")] // convert '=' to '~'
        // additional state only
        [InlineData(null, null, null, "key1=value1,key2=value2", "dd=s:1;p:0000000000000002,key1=value1,key2=value2")]
        // combined
        [InlineData(SamplingPriorityValues.UserKeep, "rum", null, "key1=value1", "dd=s:2;o:rum;p:0000000000000002,key1=value1")]
        [InlineData(SamplingPriorityValues.AutoReject, null, "_dd.p.a=b", "key1=value1", "dd=s:0;p:0000000000000002;t.a:b,key1=value1")]
        [InlineData(null, "rum", "_dd.p.a=b", "key1=value1", "dd=s:1;o:rum;p:0000000000000002;t.a:b,key1=value1")]
        [InlineData(SamplingPriorityValues.AutoKeep, "rum", "_dd.p.dm=-4,_dd.p.usr.id=12345", "key1=value1", "dd=s:1;o:rum;p:0000000000000002;t.dm:-4;t.usr.id:12345,key1=value1")]
        public void CreateTraceStateHeader(int? samplingPriority, string origin, string tags, string additionalState, string expected)
        {
            var propagatedTags = TagPropagation.ParseHeader(tags);

            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>(), propagatedTags)
            {
                Origin = origin,
                AdditionalW3CTraceState = additionalState
            };

            traceContext.SetSamplingPriority(samplingPriority, mechanism: null, notifyDistributedTracer: false);
            var spanContext = new SpanContext(parent: SpanContext.None, traceContext, serviceName: null, traceId: (TraceId)1, spanId: 2);

            var tracestate = W3CTraceContextPropagator.CreateTraceStateHeader(spanContext);

            tracestate.Should().Be(expected);
        }

        [Fact]
        public void CreateTraceStateHeader_WithPublicPropagatedTags()
        {
            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>());
            var spanContext = new SpanContext(parent: SpanContext.None, traceContext, serviceName: null, traceId: (TraceId)1, spanId: 2);
            var span = new Span(spanContext, DateTimeOffset.Now);

            var user = new UserDetails("12345")
            {
                PropagateId = true,
                Email = "user@example.com"
            };

            // use public APIs to add propagated tags
            span.SetTraceSamplingPriority(SamplingPriority.UserKeep); // adds "_dd.p.dm" and sampling priority
            span.SetUser(user);                                       // adds "_dd.p.usr.id"

            var tracestate = W3CTraceContextPropagator.CreateTraceStateHeader(spanContext);

            // note that "t.usr.id:MTIzNDU=" is encoded as "t.usr.id:MTIzNDU~"
            tracestate.Should().Be("dd=s:2;p:0000000000000002;t.dm:-4;t.usr.id:MTIzNDU~");
        }

        [Fact]
        public void CreateTraceStateHeader_With128Bit_TraceId()
        {
            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>());
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep);

            var traceId = new TraceId(0x1234567890abcdef, 0x1122334455667788);
            var spanContext = new SpanContext(parent: SpanContext.None, traceContext, serviceName: null, traceId: traceId, spanId: 2);

            var tracestate = W3CTraceContextPropagator.CreateTraceStateHeader(spanContext);

            // note that there is no "t.tid" propagated tag when using W3C headers
            // because the full 128-bit trace id fits in the "traceparent" header
            tracestate.Should().Be("dd=s:2;p:0000000000000002");
        }

        [Fact]
        public void Inject_IHeadersCollection()
        {
            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>(), tags: null)
            {
                Origin = "origin",
                AdditionalW3CTraceState = "key1=value1"
            };

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, mechanism: null, notifyDistributedTracer: false);
            var spanContext = new SpanContext(parent: SpanContext.None, traceContext, serviceName: null, traceId: (TraceId)123456789, spanId: 987654321, rawTraceId: null, rawSpanId: null);
            var headers = new Mock<IHeadersCollection>();

            W3CPropagator.Inject(spanContext, headers.Object);

            headers.Verify(h => h.Set("traceparent", "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:origin;p:000000003ade68b1,key1=value1"), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_IHeadersCollection_128Bit_TraceId()
        {
            var traceId = new TraceId(0x1234567890abcdef, 0x1122334455667788);
            var spanId = 1UL;

            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>(), tags: null)
            {
                Origin = "origin",
                AdditionalW3CTraceState = "key1=value1"
            };

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, mechanism: null, notifyDistributedTracer: false);
            var spanContext = new SpanContext(parent: SpanContext.None, traceContext, serviceName: null, traceId, spanId, rawTraceId: traceId.ToString(), rawSpanId: spanId.ToString("x16"));
            var headers = new Mock<IHeadersCollection>();

            W3CPropagator.Inject(spanContext, headers.Object);

            headers.Verify(h => h.Set("traceparent", "00-1234567890abcdef1122334455667788-0000000000000001-01"), Times.Once());
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:origin;p:0000000000000001,key1=value1"), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            var traceContext = new TraceContext(Mock.Of<IDatadogTracer>(), tags: null)
            {
                Origin = "origin",
                AdditionalW3CTraceState = "key1=value1"
            };

            traceContext.SetSamplingPriority(SamplingPriorityValues.UserKeep, mechanism: null, notifyDistributedTracer: false);
            var spanContext = new SpanContext(parent: SpanContext.None, traceContext, serviceName: null, traceId: (TraceId)123456789, spanId: 987654321, rawTraceId: null, rawSpanId: null);
            var headers = new Mock<IHeadersCollection>();

            W3CPropagator.Inject(spanContext, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set("traceparent", "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:origin;p:000000003ade68b1,key1=value1"), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("00-000000000000000000000000075bcd15-000000003ade68b1-00", 0, 123456789, 987654321, false, "000000000000000000000000075bcd15", "000000003ade68b1")]
        [InlineData("00-0000000000000000000000003ade68b1-00000000075bcd15-01", 0, 987654321, 123456789, true, "0000000000000000000000003ade68b1", "00000000075bcd15")]
        [InlineData("00-1234567890abcdef1122334455667788-00000000075bcd15-01", 0x1234567890abcdef, 0x1122334455667788, 123456789, true, "1234567890abcdef1122334455667788", "00000000075bcd15")] // 128-bit trace id
        [InlineData("01-0000000000000000000000003ade68b1-00000000075bcd15-01", 0, 987654321, 123456789, true, "0000000000000000000000003ade68b1", "00000000075bcd15")]                           // allow other versions, version=01
        [InlineData("02-000000000000000000000000075bcd15-000000003ade68b1-00-1234", 0, 123456789, 987654321, false, "000000000000000000000000075bcd15", "000000003ade68b1")]                     // allow more data after trace-flags if the version is not 00
        [InlineData("00-0000000000000000000000003ade68b1-00000000075bcd15-02", 0, 987654321, 123456789, false, "0000000000000000000000003ade68b1", "00000000075bcd15")]                          // allow unknown flags, trace-flags=02, sampled=false
        [InlineData("00-0000000000000000000000003ade68b1-00000000075bcd15-03", 0, 987654321, 123456789, true, "0000000000000000000000003ade68b1", "00000000075bcd15")]                           // allow unknown flags, trace-flags=03, sampled=true
        public void TryParseTraceParent(
            string header,
            ulong traceIdUpper,
            ulong traceIdLower,
            ulong spanId,
            bool sampled,
            string rawTraceId,
            string rawParentId)
        {
            var expected = new W3CTraceParent(
                traceId: new TraceId(traceIdUpper, traceIdLower),
                parentId: spanId,
                sampled: sampled,
                rawTraceId: rawTraceId,
                rawParentId: rawParentId);

            W3CTraceContextPropagator.TryParseTraceParent(header, out var traceParent).Should().BeTrue();

            traceParent.Should().BeEquivalentTo(expected);
        }

        // "{version:2}-{traceid:32}-{parentid:16}-{traceflags:2}
        [Theory]
        [InlineData(null)]                                                           // null
        [InlineData("")]                                                             // empty
        [InlineData(" ")]                                                            // whitespace
        [InlineData("00-000000000000000000000000075bcd15-000000003ade68b1-0")]       // too short (length => 54)
        [InlineData("000-00000000000000000000000075bcd15-000000003ade68b1-00")]      // wrong hyphen location (2 => 3)
        [InlineData("00-000000000000000000000000075bcd150-00000003ade68b1-00")]      // wrong hyphen location (35 => 36)
        [InlineData("00-000000000000000000000000075bcd15-000000003ade68b-100")]      // wrong hyphen location (52 => 51)
        [InlineData("ff-000000000000000000000000075bcd1z-000000003ade68b1-00")]      // bad version value (ff)
        [InlineData("xz-000000000000000000000000075bcd1z-000000003ade68b1-00")]      // bad version value (xz)
        [InlineData("00-000000000000000000000000075bcd1z-000000003ade68b1-00")]      // bad trace id value ("z" in hex)
        [InlineData("00-000000000000000000000000075bcd15-000000003ade68bx-00")]      // bad parent id value ("x" in hex)
        [InlineData("00-12345678901234567890123456789012-1234567890123456-.0")]      // bad value in the first trace flags character
        [InlineData("00-12345678901234567890123456789012-1234567890123456-0.")]      // bad value in the second trace flags character
        [InlineData("00-000000000000000000000000075bcd15-000000003ade68b1-00-1234")] // do NOT allow more data after trace-flags if the version is "00"
        public void TryParseTraceParent_Invalid(string header)
        {
            W3CTraceContextPropagator.TryParseTraceParent(header, out _).Should().BeFalse();
        }

        [Theory]
        // valid
        [InlineData("dd=s:2", 2, null, null, null, ZeroLastParentId)]                                                                                                       // sampling priority
        [InlineData("dd=s:-1", -1, null, null, null, ZeroLastParentId)]                                                                                                     // sampling priority
        [InlineData("dd=o:rum", null, "rum", null, null, ZeroLastParentId)]                                                                                                 // origin
        [InlineData("dd=t.dm:-4;t.usr.id:12345", null, null, "_dd.p.dm=-4,_dd.p.usr.id=12345", null, ZeroLastParentId)]                                                     // propagated tags
        [InlineData("key1=value1,key2=value2", null, null, null, "key1=value1,key2=value2", ZeroLastParentId)]                                                                          // additional values
        [InlineData("key1=value1dd=,key2=value2", null, null, null, "key1=value1dd=,key2=value2", ZeroLastParentId)]                                                                    // additional values, ignore embedded "dd="
        [InlineData("dd=s:2;o:rum;t.dm:-4;t.usr.id:12345~,key1=value1", 2, "rum", "_dd.p.dm=-4,_dd.p.usr.id=12345=", "key1=value1", ZeroLastParentId)]                      // all, but p, and '~' is converted to '='
        [InlineData("dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;t.usr.id:12345~,key1=value1", 2, "rum", "_dd.p.dm=-4,_dd.p.usr.id=12345=", "key1=value1", "0123456789abcdef")] // all, and '~' is converted to '='
        // invalid "dd" value
        [InlineData(null, null, null, null, null, ZeroLastParentId)]         // null
        [InlineData("", null, null, null, null, ZeroLastParentId)]           // empty
        [InlineData(" ", null, null, null, null, ZeroLastParentId)]          // whitespace
        [InlineData("dd=", null, null, null, null, ZeroLastParentId)]        // "dd=" prefix only
        [InlineData("dd=:2", null, null, null, null, ZeroLastParentId)]      // no key
        [InlineData("dd=s:", null, null, null, null, ZeroLastParentId)]      // no value
        [InlineData("dd=s", null, null, null, null, ZeroLastParentId)]       // no colon
        [InlineData("dd=xyz:123", null, null, null, null, ZeroLastParentId)] // unknown key
        // invalid propagated tag (first)
        [InlineData("dd=s:2;o:rum;:12345;t.dm:-4", 2, "rum", "_dd.p.dm=-4", null, ZeroLastParentId)]    // no key
        [InlineData("dd=s:2;o:rum;t.usr.id:;t.dm:-4", 2, "rum", "_dd.p.dm=-4", null, ZeroLastParentId)] // no value
        [InlineData("dd=s:2;o:rum;:;t.dm:-4", 2, "rum", "_dd.p.dm=-4", null, ZeroLastParentId)]         // no key or value
        [InlineData("dd=s:2;o:rum;t.abc;t.dm:-4", 2, "rum", "_dd.p.dm=-4", null, ZeroLastParentId)]     // no colon
        // invalid propagated tag (last)
        [InlineData("dd=s:2;o:rum;t.dm:-4;:12345", 2, "rum", "_dd.p.dm=-4", null, ZeroLastParentId)]    // no key
        [InlineData("dd=s:2;o:rum;t.dm:-4;t.usr.id:", 2, "rum", "_dd.p.dm=-4", null, ZeroLastParentId)] // no value
        [InlineData("dd=s:2;o:rum;t.dm:-4;:", 2, "rum", "_dd.p.dm=-4", null, ZeroLastParentId)]         // no key or value
        [InlineData("dd=s:2;o:rum;t.dm:-4;t.abc", 2, "rum", "_dd.p.dm=-4", null, ZeroLastParentId)]     // no colon
        // multiple top-level key/value pairs
        // before "dd"
        [InlineData("key1=value1,key2=value2,dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;t.usr.id:12345", 2, "rum", "_dd.p.dm=-4,_dd.p.usr.id=12345", "key1=value1,key2=value2", "0123456789abcdef")]
        // after "dd"
        [InlineData("dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;t.usr.id:12345,key3=value3,key4=value4", 2, "rum", "_dd.p.dm=-4,_dd.p.usr.id=12345", "key3=value3,key4=value4", "0123456789abcdef")]
        // both sides
        [InlineData("key1=value1,key2=value2,dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;t.usr.id:12345,key3=value3,key4=value4", 2, "rum", "_dd.p.dm=-4,_dd.p.usr.id=12345", "key1=value1,key2=value2,key3=value3,key4=value4", "0123456789abcdef")]
        public void ParseTraceState(string header, int? samplingPriority, string origin, string propagatedTags, string additionalValues, string lastParent)
        {
            var traceState = W3CTraceContextPropagator.ParseTraceState(header);
            var expected = new W3CTraceState(samplingPriority, origin, lastParent, propagatedTags, additionalValues);
            traceState.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void ParseTraceStateWithLastParent()
        {
            var header = "dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;t.usr.id:12345~,key1=value1";
            var traceState = W3CTraceContextPropagator.ParseTraceState(header);
            var samplingPriority = 2;
            var origin = "rum";
            var lastParent = "0123456789abcdef";
            var propagatedTags = "_dd.p.dm=-4,_dd.p.usr.id=12345=";
            var additionalValues = "key1=value1";
            var expected = new W3CTraceState(samplingPriority, origin, lastParent, propagatedTags, additionalValues);
            traceState.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void MissingLastParentId_ShouldBe_Zeroes()
        {
            var header = "dd=s:2;o:rum;t.dm:-4;t.usr.id:12345~,key1=value1";
            var traceState = W3CTraceContextPropagator.ParseTraceState(header);
            var samplingPriority = 2;
            var origin = "rum";
            var lastParent = ZeroLastParentId;
            var propagatedTags = "_dd.p.dm=-4,_dd.p.usr.id=12345=";
            var additionalValues = "key1=value1";
            var expected = new W3CTraceState(samplingPriority, origin, lastParent, propagatedTags, additionalValues);
            traceState.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void Extract_IHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:2;o:rum;t.dm:-4;t.usr.id:12345" });

            var result = W3CPropagator.Extract(headers.Object);

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
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Theory]
        [InlineData(null)]
        [InlineData(null, null)]
        [InlineData(null, null, null)]
        [InlineData("")]
        [InlineData("", "")]
        [InlineData("", "", "")]
        [InlineData("   ")]
        [InlineData("   ", "   ")]
        [InlineData("   ", "   ", "   ")]
        public void Extract_IHeadersCollection_HandlesMultipleEmptyTraceState(params string[] traceState)
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate")).Returns(traceState);

            var result = W3CPropagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("traceparent"), Times.Once());
            headers.Verify(h => h.GetValues("tracestate"), Times.AtMost(1));
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
                           SamplingPriority = 1,
                           Origin = null,
                           PropagatedTags = EmptyPropagatedTags,
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Fact]
        public void Extract_CarrierAndDelegate()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:2;o:rum;t.dm:-4;t.usr.id:12345" });

            var result = W3CPropagator.Extract(headers.Object, (carrier, name) => carrier.GetValues(name));

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
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId
                       });
        }

        [Fact]
        public void Extract_Multiple_TraceParent()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(
                        new[]
                        {
                            // multiple "traceparent" should be rejected as invalid
                            "00-000000000000000000000000075bcd15-000000003ade68b1-01",
                            "00-000000000000000000000000075bcd15-000000003ade68b1-01"
                        });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:2;o:rum;t.dm:-4;t.usr.id:12345" });

            var result = W3CPropagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("traceparent"), Times.Once());
            headers.Verify(h => h.GetValues("tracestate"), Times.Never());
            headers.VerifyNoOtherCalls();

            result.Should().BeNull();
        }

        [Fact]
        public void Extract_Multiple_TraceState()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(
                        new[]
                        {
                            // multiple "tracestate" headers should be joined
                            "abc=123",
                            "dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;t.usr.id:12345",
                            "foo=bar"
                        });

            var result = W3CPropagator.Extract(headers.Object);

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
                           AdditionalW3CTraceState = "abc=123,foo=bar",
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = "0123456789abcdef",
                       });
        }

        [Fact]
        public void Extract_No_TraceState()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(Array.Empty<string>());

            var result = W3CPropagator.Extract(headers.Object);

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
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                           Origin = null,
                           PropagatedTags = EmptyPropagatedTags,
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Fact]
        public void StringifyAndParse_PreserveOriginalTraceId()
        {
            const string traceId = "0af7651916cd43dd8448eb211c80319c";
            const string parentId = "00f067aa0ba902b7";
            const string traceParentHeader = $"00-{traceId}-{parentId}-01";

            W3CTraceContextPropagator.TryParseTraceParent(traceParentHeader, out var traceParent)
                                     .Should().BeTrue();

            var expectedTraceId = new TraceId(0x0af7651916cd43dd, 0x8448eb211c80319c);
            const ulong expectedParentId = 0x00f067aa0ba902b7;

            traceParent.Should()
                       .BeEquivalentTo(
                            new W3CTraceParent(
                                expectedTraceId,
                                expectedParentId,
                                sampled: true,
                                rawTraceId: traceId,
                                rawParentId: parentId));

            var spanContext = new SpanContext(
                traceParent.TraceId,
                traceParent.ParentId,
                samplingPriority: SamplingPriorityValues.AutoKeep,
                serviceName: null,
                origin: null,
                traceParent.RawTraceId,
                traceParent.RawParentId);

            W3CTraceContextPropagator.CreateTraceParentHeader(spanContext)
                                     .Should().Be(traceParentHeader);
        }

        [Theory]
        // no replacements
        [InlineData("valid1234567890", false)]
        [InlineData("`~!@#$%^&*(){}[]<>-_+'\"/|\\?.", false)]
        // specific replacements
        [InlineData(",foo,bar,", true)]
        [InlineData(";foo;bar;", true)]
        [InlineData("=foo=bar=", true)]
        [InlineData(",foo;bar=", true)]
        // out of bounds replacements
        [InlineData("\0foo\tbar\u00E7", true)]
        [InlineData("dog🐶", true)]
        public void NeedsCharacterReplacement(string value, bool expected)
        {
            const char lowerBound = '\u0020'; // decimal: 32, ' ' (space)
            const char upperBound = '\u007e'; // decimal: 126, '~' (tilde)

            KeyValuePair<char, char>[] invalidCharacterReplacements =
            {
                new(',', '1'),
                new(';', '2'),
                new('=', '3'),
            };

            W3CTraceContextPropagator.NeedsCharacterReplacement(value, lowerBound, upperBound, invalidCharacterReplacements)
                                     .Should()
                                     .Be(expected);
        }

        [Theory]
        // no replacements
        [InlineData("valid1234567890", "valid1234567890")]
        [InlineData("`~!@#$%^&*(){}[]<>-_+'\"/|\\?.", "`~!@#$%^&*(){}[]<>-_+'\"/|\\?.")]
        // specific replacements
        [InlineData(",foo,bar,", "1foo1bar1")]
        [InlineData(";foo;bar;", "2foo2bar2")]
        [InlineData("=foo=bar=", "3foo3bar3")]
        [InlineData(",foo;bar=", "1foo2bar3")]
        // out of bounds replacements
        [InlineData("\0foo\tbar\u00E7", "_foo_bar_")]
        [InlineData("dog🐶", "dog__")] // note that 🐶 is two UTF-16 chars, can also be written as "dog\ud83d\udc36" (UTF-16) or "dog\U0001F436" (UTF-32)
        public void ReplaceInvalidCharacters(string value, string expected)
        {
            const char lowerBound = '\u0020'; // decimal: 32, ' ' (space)
            const char upperBound = '\u007e'; // decimal: 126, '~' (tilde)
            const char outOfBoundsReplacement = '_';

            KeyValuePair<char, char>[] invalidCharacterReplacements =
            {
                new(',', '1'),
                new(';', '2'),
                new('=', '3'),
            };

            W3CTraceContextPropagator.ReplaceCharacters(value, lowerBound, upperBound, outOfBoundsReplacement, invalidCharacterReplacements)
                                     .ToString() // NETCOREAPP returns ReadOnlySpan<char>
                                     .Should()
                                     .Be(expected);
        }

        [Theory]
        [InlineData(SamplingPriorityValues.AutoKeep)]
        [InlineData(SamplingPriorityValues.UserKeep)]
        [InlineData(3)]
        public void Extract_MatchingSampled1_UsesTracestateSamplingPriority(int samplingPriority)
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { $"dd=s:{samplingPriority}" });

            var result = W3CPropagator.Extract(headers.Object);

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
                           SamplingPriority = samplingPriority,
                           Origin = null,
                           PropagatedTags = EmptyPropagatedTags,
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Theory]
        [InlineData(SamplingPriorityValues.AutoReject)]
        [InlineData(SamplingPriorityValues.UserReject)]
        [InlineData(-2)]
        public void Extract_MatchingSampled0_UsesTracestateSamplingPriority(int samplingPriority)
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-00" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { $"dd=s:{samplingPriority}" });

            var result = W3CPropagator.Extract(headers.Object);

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
                           SamplingPriority = samplingPriority,
                           Origin = null,
                           PropagatedTags = EmptyPropagatedTags,
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Fact]
        public void Extract_MismatchingSampled1_UsesSamplingPriorityOf1()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:-1" });

            var result = W3CPropagator.Extract(headers.Object);

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
                           SamplingPriority = 1,
                           Origin = null,
                           PropagatedTags = new(
                               new List<KeyValuePair<string, string>>
                               {
                                   new("_dd.p.dm", "-0"),
                               },
                               cachedPropagationHeader: null),
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Fact]
        public void Extract_MismatchingSampled1_Resets_DecisionMaker()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:-1;t.dm:-4" });

            var result = W3CPropagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("traceparent"), Times.Once());
            headers.Verify(h => h.GetValues("tracestate"), Times.Once());
            headers.VerifyNoOtherCalls();

            result.Should()
                  .NotBeNull()
                  .And
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = new TraceId(0x0000000000000000, 0x00000000075bcd15),
                           TraceId = 0x00000000075bcd15,
                           SpanId = 987654321,
                           RawTraceId = "000000000000000000000000075bcd15",
                           RawSpanId = "000000003ade68b1",
                           SamplingPriority = 1,
                           Origin = null,
                           PropagatedTags = new(
                               new List<KeyValuePair<string, string>>
                               {
                                   new("_dd.p.dm", "-0"),
                               },
                               cachedPropagationHeader: null),
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Fact]
        public void Extract_MismatchingSampled0_UsesSamplingPriorityOf0()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-00" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:1" });

            var result = W3CPropagator.Extract(headers.Object);

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
                           SamplingPriority = 0,
                           Origin = null,
                           PropagatedTags = EmptyPropagatedTags,
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Fact]
        public void Extract_MismatchingSampled0_Resets_DecisionMaker()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-00" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(new[] { "dd=s:2;t.dm:-4" });

            var result = W3CPropagator.Extract(headers.Object);

            headers.Verify(h => h.GetValues("traceparent"), Times.Once());
            headers.Verify(h => h.GetValues("tracestate"), Times.Once());
            headers.VerifyNoOtherCalls();

            result.Should()
                  .NotBeNull()
                  .And
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = new TraceId(0x0000000000000000, 0x00000000075bcd15),
                           TraceId = 0x00000000075bcd15,
                           SpanId = 987654321,
                           RawTraceId = "000000000000000000000000075bcd15",
                           RawSpanId = "000000003ade68b1",
                           SamplingPriority = 0,
                           Origin = null,
                           PropagatedTags = EmptyPropagatedTags,
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Fact]
        public void Extract_Sampled1_MissingSamplingPriority_UsesSamplingPriorityOf1()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-01" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(Array.Empty<string>());

            var result = W3CPropagator.Extract(headers.Object);

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
                           SamplingPriority = 1,
                           Origin = null,
                           PropagatedTags = EmptyPropagatedTags,
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }

        [Fact]
        public void Extract_Sampled0_MissingSamplingPriority_UsesSamplingPriorityOf0()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            headers.Setup(h => h.GetValues("traceparent"))
                   .Returns(new[] { "00-000000000000000000000000075bcd15-000000003ade68b1-00" });

            headers.Setup(h => h.GetValues("tracestate"))
                   .Returns(Array.Empty<string>());

            var result = W3CPropagator.Extract(headers.Object);

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
                           SamplingPriority = 0,
                           Origin = null,
                           PropagatedTags = EmptyPropagatedTags,
                           IsRemote = true,
                           Parent = null,
                           ParentId = null,
                           LastParentId = ZeroLastParentId,
                       });
        }
    }
}
