// <copyright file="DatadogPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class DatadogPropagatorTests
    {
        private const ulong TraceId = 1;
        private const ulong SpanId = 2;
        private const int SamplingPriority = SamplingPriorityValues.UserReject;
        private const string Origin = "origin";
        private const string PropagatedTags = "key1=value1;key2=value2";

        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        private static readonly SpanContextPropagator Propagator;

        private static readonly KeyValuePair<string, string>[] DefaultHeaderValues =
        {
            new("x-datadog-trace-id", TraceId.ToString(InvariantCulture)),
            new("x-datadog-parent-id", SpanId.ToString(InvariantCulture)),
            new("x-datadog-sampling-priority", SamplingPriority.ToString(InvariantCulture)),
            new("x-datadog-origin", Origin),
            new("x-datadog-tags", PropagatedTags),
        };

        static DatadogPropagatorTests()
        {
            Propagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
                new[] { ContextPropagationHeaderStyle.Datadog },
                new[] { ContextPropagationHeaderStyle.Datadog });
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
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { PropagatedTags = PropagatedTags };
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            VerifySetCalls(headers);
        }

        [Fact]
        public void Inject_IHeadersCollection_Propagation_Disabled()
        {
            KeyValuePair<string, string>[] expectedHeaders =
                {
                    new("x-datadog-trace-id", TraceId.ToString(InvariantCulture)),
                    new("x-datadog-parent-id", SpanId.ToString(InvariantCulture)),
                };

            var settings = new TracerSettings { OutgoingTagPropagationHeaderMaxLength = 0 };
            var traceContext = new TraceContext(new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, null, telemetry: null));
            var context = new SpanContext(null, traceContext, serviceName: null, TraceId, SpanId) { PropagatedTags = PropagatedTags };

            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);
            VerifySetCalls(headers, expectedHeaders);
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { PropagatedTags = PropagatedTags };

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            VerifySetCalls(headers);
        }

        [Fact]
        public void Inject_TraceIdSpanIdOnly()
        {
            var context = new SpanContext(TraceId, SpanId, samplingPriority: null, serviceName: null, origin: null) { PropagatedTags = null };
            var headers = new Mock<IHeadersCollection>();

            Propagator.Inject(context, headers.Object);

            // null values are not set, so only traceId and spanId (the first two in the list) should be set
            headers.Verify(h => h.Set("x-datadog-trace-id", TraceId.ToString(InvariantCulture)), Times.Once());
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
            headers.Verify(h => h.Set("x-datadog-trace-id", TraceId.ToString(InvariantCulture)), Times.Once());
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
                           TraceId = TraceId,
                           SpanId = SpanId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority,
                           PropagatedTags = PropagatedTags,
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
                           TraceId = TraceId,
                           SpanId = SpanId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority,
                           PropagatedTags = PropagatedTags,
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
                           TraceId = TraceId,
                           SpanId = SpanId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority,
                           PropagatedTags = PropagatedTags,
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
            headers.Setup(h => h.GetValues("x-datadog-trace-id")).Returns(new[] { TraceId.ToString(InvariantCulture) });
            var result = Propagator.Extract(headers.Object);

            result.Should().BeEquivalentTo(new SpanContextMock { TraceId = TraceId });
        }

        [Fact]
        public void SpanContextRoundTrip()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { PropagatedTags = PropagatedTags };
            var result = Propagator.Extract(context);

            result.Should().NotBeSameAs(context);
            result.Should().BeEquivalentTo(context);
        }

        [Fact]
        public void Identity()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { PropagatedTags = PropagatedTags };
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
                           TraceId = TraceId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority,
                           PropagatedTags = PropagatedTags,
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
                           TraceId = TraceId,
                           SpanId = SpanId,
                           Origin = Origin,
                           SamplingPriority = expectedSamplingPriority,
                           PropagatedTags = PropagatedTags,
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

        private static void VerifySetCalls(Mock<IHeadersCollection> headers, KeyValuePair<string, string>[] headersToCheck = null)
        {
            var once = Times.Once();

            foreach (var pair in headersToCheck ?? DefaultHeaderValues)
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

#pragma warning disable SA1402 // File may only contain a single type
    // used to compare property values
    internal class SpanContextMock
    {
        public ulong TraceId { get; set; }

        public ulong SpanId { get; set; }

        public string RawTraceId { get; set; }

        public string RawSpanId { get; set; }

        public string Origin { get; set; }

        public int? SamplingPriority { get; set; }

        public string PropagatedTags { get; set; }

        public string AdditionalW3CTraceState { get; set; }

        public ISpanContext Parent { get; set; }

        public ulong? ParentId { get; set; }

        public string ServiceName { get; set; }

        public TraceContext TraceContext { get; set; }
    }
#pragma warning restore SA1402 // File may only contain a single type
}
