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
        private static readonly SpanContextPropagator W3CPropagator;

        static W3CTraceContextPropagatorTests()
        {
            W3CPropagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[] { ContextPropagationHeaderStyle.W3CTraceContext },
                new[] { ContextPropagationHeaderStyle.W3CTraceContext });
        }

        [Theory]
        [InlineData(123456789, 987654321, null, "00-000000000000000000000000075bcd15-000000003ade68b1-01")]
        [InlineData(123456789, 987654321, SamplingPriorityValues.UserReject, "00-000000000000000000000000075bcd15-000000003ade68b1-00")]
        [InlineData(123456789, 987654321, SamplingPriorityValues.AutoReject, "00-000000000000000000000000075bcd15-000000003ade68b1-00")]
        [InlineData(123456789, 987654321, SamplingPriorityValues.AutoKeep, "00-000000000000000000000000075bcd15-000000003ade68b1-01")]
        [InlineData(123456789, 987654321, SamplingPriorityValues.UserKeep, "00-000000000000000000000000075bcd15-000000003ade68b1-01")]
        public void CreateTraceParentHeader(ulong traceId, ulong spanId, int? samplingPriority, string expected)
        {
            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: null, "origin");
            var traceparent = W3CTraceContextPropagator.CreateTraceParentHeader(context);

            traceparent.Should().Be(expected);
        }

        [Theory]
        // null/empty/whitespace
        [InlineData(null, null, null, "")]
        [InlineData(null, "", "", "")]
        [InlineData(null, " ", " ", "")]
        // sampling priority only
        [InlineData(SamplingPriorityValues.UserReject, null, null, "dd=s:-1")]
        [InlineData(SamplingPriorityValues.AutoReject, null, null, "dd=s:0")]
        [InlineData(SamplingPriorityValues.AutoKeep, null, null, "dd=s:1")]
        [InlineData(SamplingPriorityValues.UserKeep, null, null, "dd=s:2")]
        [InlineData(3, null, null, "dd=s:3")]
        [InlineData(-5, null, null, "dd=s:-5")]
        // origin only
        [InlineData(null, "abc", null, "dd=o:abc")]
        // propagated tags only
        [InlineData(null, null, "_dd.p.a=1", "dd=t.a:1")]
        [InlineData(null, null, "_dd.p.a=1,_dd.p.b=2", "dd=t.a:1;t.b:2")]
        [InlineData(null, null, "_dd.p.a=1,b=2", "dd=t.a:1")]
        [InlineData(null, null, "_dd.p.usr.id=MTIzNDU=", "dd=t.usr.id:MTIzNDU~")] // convert '=' to '~'
        // combined
        [InlineData(SamplingPriorityValues.UserKeep, "rum", null, "dd=s:2;o:rum")]
        [InlineData(SamplingPriorityValues.AutoReject, null, "_dd.p.a=b", "dd=s:0;t.a:b")]
        [InlineData(null, "rum", "_dd.p.a=b", "dd=o:rum;t.a:b")]
        [InlineData(SamplingPriorityValues.AutoKeep, "rum", "_dd.p.dm=-4,_dd.p.usr.id=12345", "dd=s:1;o:rum;t.dm:-4;t.usr.id:12345")]
        public void CreateTraceStateHeader(int? samplingPriority, string origin, string tags, string expected)
        {
            var propagatedTags = TagPropagation.ParseHeader(tags, 100);
            var traceContext = new TraceContext(tracer: null, propagatedTags) { Origin = origin };
            traceContext.SetSamplingPriority(samplingPriority, mechanism: null, notifyDistributedTracer: false);
            var spanContext = new SpanContext(parent: null, traceContext, serviceName: null, traceId: 1, spanId: 2);

            var tracestate = W3CTraceContextPropagator.CreateTraceStateHeader(spanContext);

            tracestate.Should().Be(expected);
        }

        [Fact]
        public void CreateTraceStateHeader_WithPublicPropagatedTags()
        {
            var traceContext = new TraceContext(tracer: null);
            var spanContext = new SpanContext(parent: null, traceContext, serviceName: null, traceId: 1, spanId: 2);
            var span = new Span(spanContext, DateTimeOffset.Now);

            var user = new UserDetails("12345")
                       {
                           PropagateId = true,
                           Email = "user@example.com"
                       };

            // use public APIs to add propagated tags
            span.SetTraceSamplingPriority(SamplingPriority.UserKeep); // adds "_dd.p.dm"
            span.SetUser(user);                                       // adds "_dd.p.usr.id"

            var tracestate = W3CTraceContextPropagator.CreateTraceStateHeader(spanContext);

            // note that "t.usr.id:MTIzNDU=" is encoded as "t.usr.id:MTIzNDU~"
            tracestate.Should().Be("dd=s:2;t.dm:-4;t.usr.id:MTIzNDU~");
        }

        [Fact]
        public void Inject_IHeadersCollection()
        {
            var context = new SpanContext(
                traceId: 123456789,
                spanId: 987654321,
                SamplingPriorityValues.UserKeep,
                serviceName: null,
                "origin");

            var headers = new Mock<IHeadersCollection>();

            W3CPropagator.Inject(context, headers.Object);

            headers.Verify(h => h.Set("traceparent", "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:origin"), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            var context = new SpanContext(
                traceId: 123456789,
                spanId: 987654321,
                SamplingPriorityValues.UserKeep,
                serviceName: null,
                "origin");

            var headers = new Mock<IHeadersCollection>();

            W3CPropagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            headers.Verify(h => h.Set("traceparent", "00-000000000000000000000000075bcd15-000000003ade68b1-01"), Times.Once());
            headers.Verify(h => h.Set("tracestate", "dd=s:2;o:origin"), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("00-000000000000000000000000075bcd15-000000003ade68b1-00", 123456789, 987654321, false, "000000000000000000000000075bcd15", "000000003ade68b1")]
        [InlineData("00-0000000000000000000000003ade68b1-00000000075bcd15-01", 987654321, 123456789, true, "0000000000000000000000003ade68b1", "00000000075bcd15")]
        [InlineData("01-0000000000000000000000003ade68b1-00000000075bcd15-01", 987654321, 123456789, true, "0000000000000000000000003ade68b1", "00000000075bcd15")]       // allow other versions, version=01
        [InlineData("02-000000000000000000000000075bcd15-000000003ade68b1-00-1234", 123456789, 987654321, false, "000000000000000000000000075bcd15", "000000003ade68b1")] // allow more data after trace-flags if the version is not 00
        [InlineData("00-0000000000000000000000003ade68b1-00000000075bcd15-02", 987654321, 123456789, false, "0000000000000000000000003ade68b1", "00000000075bcd15")]      // allow unknown flags, trace-flags=02, sampled=false
        [InlineData("00-0000000000000000000000003ade68b1-00000000075bcd15-03", 987654321, 123456789, true, "0000000000000000000000003ade68b1", "00000000075bcd15")]       // allow unknown flags, trace-flags=03, sampled=true
        public void TryParseTraceParent(string header, ulong traceId, ulong spanId, bool sampled, string rawTraceId, string rawParentId)
        {
            var expected = new W3CTraceParent(
                traceId,
                spanId,
                sampled,
                rawTraceId,
                rawParentId);

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
        [InlineData("00-000000000000000000000000075bcd15-000000003ade68b1-00-1234")] // do NOT allow more data after trace-flags if the version is "00"
        public void TryParseTraceParent_Invalid(string header)
        {
            W3CTraceContextPropagator.TryParseTraceParent(header, out _).Should().BeFalse();
        }

        [Theory]
        [InlineData("dd=s:2", 2, null, null)]
        [InlineData("dd=o:rum", null, "rum", null)]
        [InlineData("dd=t.dm:-4;t.usr.id:12345", null, null, "_dd.p.dm=-4,_dd.p.usr.id=12345")]
        [InlineData("dd=s:2;o:rum;t.dm:-4;t.usr.id:12345~", 2, "rum", "_dd.p.dm=-4,_dd.p.usr.id=12345=")] // '~' is converted to '='
        // invalid propagated tag (first)
        [InlineData("dd=s:2;o:rum;:12345;t.dm:-4", 2, "rum", "_dd.p.dm=-4")]    // no key
        [InlineData("dd=s:2;o:rum;t.usr.id:;t.dm:-4", 2, "rum", "_dd.p.dm=-4")] // no value
        [InlineData("dd=s:2;o:rum;:;t.dm:-4", 2, "rum", "_dd.p.dm=-4")]         // no key or value
        [InlineData("dd=s:2;o:rum;t.abc;t.dm:-4", 2, "rum", "_dd.p.dm=-4")]     // no colon
        // invalid propagated tag (last)
        [InlineData("dd=s:2;o:rum;t.dm:-4;:12345", 2, "rum", "_dd.p.dm=-4")]    // no key
        [InlineData("dd=s:2;o:rum;t.dm:-4;t.usr.id:", 2, "rum", "_dd.p.dm=-4")] // no value
        [InlineData("dd=s:2;o:rum;t.dm:-4;:", 2, "rum", "_dd.p.dm=-4")]         // no key or value
        [InlineData("dd=s:2;o:rum;t.dm:-4;t.abc", 2, "rum", "_dd.p.dm=-4")]     // no colon
        // multiple top-level key/value pairs
        [InlineData("abc=123,dd=s:2;o:rum;t.dm:-4;t.usr.id:12345,foo=bar", 2, "rum", "_dd.p.dm=-4,_dd.p.usr.id=12345")]
        public void TryParseTraceState(string header, int? samplingPriority, string origin, string propagatedTags)
        {
            var expected = new W3CTraceState(samplingPriority, origin, propagatedTags);

            W3CTraceContextPropagator.TryParseTraceState(header, out var traceState).Should().BeTrue();

            traceState.Should().NotBeNull().And.BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData(null)]        // null
        [InlineData("")]          // empty
        [InlineData(" ")]         // whitespace
        [InlineData("s:2;o:rum")] // missing "dd=" prefix
        [InlineData("dd=")]       // "dd=" prefix only
        public void TryParseTraceState_Invalid(string header)
        {
            W3CTraceContextPropagator.TryParseTraceState(header, out _).Should().BeFalse();
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
                           TraceId = 123456789,
                           SpanId = 987654321,
                           SamplingPriority = SamplingPriorityValues.UserKeep,
                           Origin = "rum",
                           PropagatedTags = "_dd.p.dm=-4,_dd.p.usr.id=12345",
                           Parent = null,
                           ParentId = null,
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
                           TraceId = 123456789,
                           SpanId = 987654321,
                           SamplingPriority = SamplingPriorityValues.UserKeep,
                           Origin = "rum",
                           PropagatedTags = "_dd.p.dm=-4,_dd.p.usr.id=12345",
                           Parent = null,
                           ParentId = null,
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
                            "dd=s:2;o:rum;t.dm:-4;t.usr.id:12345",
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
                           TraceId = 123456789,
                           SpanId = 987654321,
                           SamplingPriority = SamplingPriorityValues.UserKeep,
                           Origin = "rum",
                           PropagatedTags = "_dd.p.dm=-4,_dd.p.usr.id=12345",
                           Parent = null,
                           ParentId = null,
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
                           TraceId = 123456789,
                           SpanId = 987654321,
                           SamplingPriority = SamplingPriorityValues.AutoKeep,
                           Origin = null,
                           PropagatedTags = null,
                           Parent = null,
                           ParentId = null,
                       });
        }

        [Fact]
        public void StringifyAndParse_PreserveOriginalTraceId()
        {
            const string traceId = "0af7651916cd43dd8448eb211c80319c";
            const string parentId = "00f067aa0ba902b7";
            const string traceParentHeader = $"00-{traceId}-{parentId}-01";

            W3CTraceContextPropagator.TryParseTraceParent(traceParentHeader, out var traceParent).Should().BeTrue();

            // 64 bits verify
            const ulong expectedTraceId = 9532127138774266268UL;
            const ulong expectedParentId = 67667974448284343UL;

            traceParent.Should()
                       .NotBeNull()
                       .And
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

            W3CTraceContextPropagator.CreateTraceParentHeader(spanContext).Should().Be(traceParentHeader);
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
    }
}
