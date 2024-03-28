// <copyright file="DatadogPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class DatadogPropagatorTests
    {
        private const int SamplingPriority = SamplingPriorityValues.UserReject;
        private const string Origin = "origin";
        private const string PropagatedTagsString = "_dd.p.key1=value1,_dd.p.key2=value2";
        private const ulong SpanId = 0x1122334455667788;                       // 1234605616436508552
        private const string RawSpanId = "1122334455667788";                   // 1234605616436508552
        private const string RawTraceId = "00000000000000001234567890abcdef";  // 1311768467294899695
        private static readonly TraceId TraceId = (TraceId)0x1234567890abcdef; // 1311768467294899695

        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        private static readonly SpanContextPropagator Propagator;

        private static readonly KeyValuePair<string, string>[] DefaultHeaderValues =
        {
            new("x-datadog-trace-id", TraceId.Lower.ToString(InvariantCulture)),
            new("x-datadog-parent-id", SpanId.ToString(InvariantCulture)),
            new("x-datadog-sampling-priority", SamplingPriority.ToString(InvariantCulture)),
            new("x-datadog-origin", Origin),
            new("x-datadog-tags", PropagatedTagsString),
        };

        private static readonly TraceTagCollection PropagatedTagsCollection = new(
            new List<KeyValuePair<string, string>>
            {
                new("_dd.p.key1", "value1"),
                new("_dd.p.key2", "value2"),
            },
            PropagatedTagsString);

        private static readonly TraceTagCollection EmptyPropagatedTags = new();

        static DatadogPropagatorTests()
        {
            Propagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[] { ContextPropagationHeaderStyle.Datadog },
                new[] { ContextPropagationHeaderStyle.Datadog },
                false);
        }

        public static TheoryData<string> GetInvalidIds() => new()
        {
            null,
            string.Empty,
            "0",
            "-1",
            "id",
        };

        [Fact]
        public void Inject_IHeadersCollection()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { PropagatedTags = PropagatedTagsCollection };
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            VerifySetCalls(headers, DefaultHeaderValues);
        }

        [Fact]
        public void Inject_IHeadersCollection_Tag_Propagation_Disabled()
        {
            KeyValuePair<string, string>[] expectedHeaders =
                {
                    new("x-datadog-trace-id", TraceId.Lower.ToString(InvariantCulture)),
                    new("x-datadog-parent-id", SpanId.ToString(InvariantCulture)),
                    new("x-datadog-sampling-priority", SamplingPriority.ToString(InvariantCulture)),
                    new("x-datadog-origin", Origin),
                };

            var settings = TracerSettings.Create(new() { { ConfigurationKeys.TagPropagation.HeaderMaxLength, 0 } });
            var tracer = new Tracer(settings, agentWriter: Mock.Of<IAgentWriter>(), sampler: null, scopeManager: null, null, telemetry: null);

            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(SamplingPriority);
            traceContext.Origin = Origin;
            traceContext.Tags.SetTags(PropagatedTagsCollection);

            var context = new SpanContext(parent: null, traceContext, serviceName: null, TraceId, SpanId);
            var headers = new Mock<IHeadersCollection>();
            Propagator.Inject(context, headers.Object);

            VerifySetCalls(headers, expectedHeaders);
        }

        [Fact]
        public void Inject_IHeadersCollection_128Bit_TraceId()
        {
            var traceId = new TraceId(0x1234567890abcdef, 0x1122334455667788);
            var spanId = 1UL;

            KeyValuePair<string, string>[] expectedHeaders =
            {
                new("x-datadog-trace-id", traceId.Lower.ToString(InvariantCulture)),
                new("x-datadog-parent-id", spanId.ToString(InvariantCulture)),
                new("x-datadog-sampling-priority", SamplingPriority.ToString(InvariantCulture)),
                new("x-datadog-origin", Origin),

                // verify that "_dd.p.tid" tag is injected for 128-bit trace ids
                new("x-datadog-tags", @"_dd.p.tid=1234567890abcdef"),
            };

            var context = new SpanContext(traceId, spanId, SamplingPriority, serviceName: null, Origin);
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            VerifySetCalls(headers, expectedHeaders);
        }

        [Fact]
        public void Inject_IHeadersCollection_64Bit_TraceId()
        {
            var traceId = new TraceId(0, 0x1122334455667788);
            var spanId = 1UL;

            KeyValuePair<string, string>[] expectedHeaders =
            {
                new("x-datadog-trace-id", traceId.Lower.ToString(InvariantCulture)),
                new("x-datadog-parent-id", spanId.ToString(InvariantCulture)),
                new("x-datadog-sampling-priority", SamplingPriority.ToString(InvariantCulture)),
                new("x-datadog-origin", Origin),
                // verify that "_dd.p.tid" tag is not injected for 64-bit trace ids
            };

            var context = new SpanContext(traceId, spanId, SamplingPriority, serviceName: null, Origin);
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            VerifySetCalls(headers, expectedHeaders);
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { PropagatedTags = PropagatedTagsCollection };

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            VerifySetCalls(headers, DefaultHeaderValues);
        }

        [Fact]
        public void Inject_TraceIdSpanIdOnly()
        {
            var context = new SpanContext(TraceId, SpanId, samplingPriority: null, serviceName: null, origin: null) { PropagatedTags = null };
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            // null values are not set, so only traceId and spanId (the first two in the list) should be set
            headers.Verify(h => h.Set("x-datadog-trace-id", TraceId.Lower.ToString(InvariantCulture)), Times.Once());
            headers.Verify(h => h.Set("x-datadog-parent-id", SpanId.ToString(InvariantCulture)), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Inject_InvalidSampling()
        {
            var context = new SpanContext(TraceId, SpanId, samplingPriority: 12, serviceName: null, origin: null);
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            // null values are not set, so only traceId and spanId (the first two in the list) should be set
            headers.Verify(h => h.Set("x-datadog-trace-id", TraceId.Lower.ToString(InvariantCulture)), Times.Once());
            headers.Verify(h => h.Set("x-datadog-parent-id", SpanId.ToString(InvariantCulture)), Times.Once());
            headers.Verify(h => h.Set("x-datadog-sampling-priority", "12"), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Extract_IHeadersCollection()
        {
            var headers = SetupMockHeadersCollection();
            var result = Propagator.Extract(headers.Object);

            VerifyGetCalls(headers);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = TraceId,
                           TraceId = TraceId.Lower,
                           RawTraceId = RawTraceId,
                           SpanId = SpanId,
                           RawSpanId = RawSpanId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority,
                           PropagatedTags = PropagatedTagsCollection,
                           IsRemote = true,
                       });
        }

        [Fact]
        public void Extract_CarrierAndDelegate()
        {
            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = SetupMockHeadersCollection();
            var result = Propagator.Extract(headers.Object, (carrier, name) => carrier.GetValues(name));

            VerifyGetCalls(headers);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = TraceId,
                           TraceId = TraceId.Lower,
                           RawTraceId = RawTraceId,
                           SpanId = SpanId,
                           RawSpanId = RawSpanId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority,
                           PropagatedTags = PropagatedTagsCollection,
                           IsRemote = true,
                       });
        }

        [Fact]
        public void Extract_ReadOnlyDictionary()
        {
            var headers = SetupMockReadOnlyDictionary();
            var result = Propagator.Extract(headers.Object);

            VerifyGetCalls(headers);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = TraceId,
                           TraceId = TraceId.Lower,
                           RawTraceId = RawTraceId,
                           SpanId = SpanId,
                           RawSpanId = RawSpanId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority,
                           PropagatedTags = PropagatedTagsCollection,
                           IsRemote = true,
                       });
        }

        [Fact]
        public void Extract_EmptyHeadersReturnsNull()
        {
            var headers = new Mock<IHeadersCollection>();
            var result = Propagator.Extract(headers.Object);

            result.Should().BeNull();
        }

        [Fact]
        public void Extract_TraceIdOnly()
        {
            var headers = new Mock<IHeadersCollection>();

            // only setup TraceId, other properties remain null/empty
            headers.Setup(h => h.GetValues("x-datadog-trace-id")).Returns(new[] { TraceId.Lower.ToString(InvariantCulture) });
            var result = Propagator.Extract(headers.Object);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = TraceId,
                           TraceId = TraceId.Lower,
                           RawTraceId = RawTraceId,
                           RawSpanId = "0000000000000000",
                           PropagatedTags = EmptyPropagatedTags,
                           IsRemote = true,
                       });
        }

        [Fact]
        public void SpanContextRoundTrip()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { PropagatedTags = PropagatedTagsCollection };
            var result = Propagator.Extract(context);

            result.Should().NotBeSameAs(context);
            result.Should().BeEquivalentTo(context);
        }

        [Fact]
        public void Identity()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { PropagatedTags = PropagatedTagsCollection };
            var headers = new NameValueHeadersCollection(new NameValueCollection());

            Propagator.Inject(context, headers);
            var result = Propagator.Extract(headers);

            result.Should().NotBeSameAs(context);
            result.Should().BeEquivalentTo(context);
        }

        [Theory]
        [MemberData(nameof(GetInvalidIds))]
        public void Extract_InvalidTraceId(string traceId)
        {
            var headers = SetupMockHeadersCollection();

            // replace TraceId setup
            headers.Setup(h => h.GetValues("x-datadog-trace-id")).Returns(new[] { traceId });

            var result = Propagator.Extract(headers.Object);

            // invalid traceId should return a null context even if other values are set
            result.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(GetInvalidIds))]
        public void Extract_InvalidSpanId(string spanId)
        {
            var headers = SetupMockHeadersCollection();

            // replace ParentId setup
            headers.Setup(h => h.GetValues("x-datadog-parent-id")).Returns(new[] { spanId });

            var result = Propagator.Extract(headers.Object);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           // SpanId has default value
                           TraceId128 = TraceId,
                           TraceId = TraceId.Lower,
                           RawTraceId = RawTraceId,
                           RawSpanId = "0000000000000000",
                           Origin = Origin,
                           SamplingPriority = SamplingPriority,
                           PropagatedTags = PropagatedTagsCollection,
                           IsRemote = true,
                       });
        }

        [Theory]
        [InlineData("-1000", -1000)]
        [InlineData("1000", 1000)]
        [InlineData("1.0", null)]
        [InlineData("1,0", null)]
        [InlineData("sampling.priority", null)]
        public void Extract_InvalidSamplingPriority(string samplingPriority, int? expectedSamplingPriority)
        {
            // if the extracted sampling priority is a valid integer, pass it along as-is,
            // even if we don't recognize its value to allow forward compatibility with newly added values.
            // ignore the extracted sampling priority if it is not a valid integer.

            var headers = SetupMockHeadersCollection();

            // replace SamplingPriority setup
            headers.Setup(h => h.GetValues("x-datadog-sampling-priority")).Returns(new[] { samplingPriority });

            object result = Propagator.Extract(headers.Object);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId128 = TraceId,
                           TraceId = TraceId.Lower,
                           RawTraceId = RawTraceId,
                           SpanId = SpanId,
                           RawSpanId = RawSpanId,
                           Origin = Origin,
                           SamplingPriority = expectedSamplingPriority,
                           PropagatedTags = PropagatedTagsCollection,
                           IsRemote = true,
                       });
        }

        private static Mock<IHeadersCollection> SetupMockHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            foreach (var pair in DefaultHeaderValues)
            {
                headers.Setup(h => h.GetValues(pair.Key)).Returns(new[] { pair.Value });
            }

            return headers;
        }

        private static Mock<IReadOnlyDictionary<string, string>> SetupMockReadOnlyDictionary()
        {
            var headers = new Mock<IReadOnlyDictionary<string, string>>();

            foreach (var pair in DefaultHeaderValues)
            {
                var value = pair.Value;
                headers.Setup(h => h.TryGetValue(pair.Key, out value)).Returns(true);
            }

            return headers;
        }

        private static void VerifySetCalls(Mock<IHeadersCollection> headers, KeyValuePair<string, string>[] headersToCheck)
        {
            var once = Times.Once();

            foreach (var pair in headersToCheck)
            {
                headers.Verify(h => h.Set(pair.Key, pair.Value), once);
            }

            headers.VerifyNoOtherCalls();
        }

        private static void VerifyGetCalls(Mock<IHeadersCollection> headers)
        {
            var once = Times.Once();

            foreach (var pair in DefaultHeaderValues)
            {
                headers.Verify(h => h.GetValues(pair.Key), once);
            }

            headers.VerifyNoOtherCalls();
        }

        private static void VerifyGetCalls(Mock<IReadOnlyDictionary<string, string>> headers)
        {
            var once = Times.Once();
            string value;

            foreach (var pair in DefaultHeaderValues)
            {
                headers.Verify(h => h.TryGetValue(pair.Key, out value), once);
            }

            headers.VerifyNoOtherCalls();
        }
    }
}
