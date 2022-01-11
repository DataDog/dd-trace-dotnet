// <copyright file="SpanContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Datadog.Trace.Headers;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public unsafe class SpanContextPropagatorTests
    {
        private const ulong TraceId = 1;
        private const ulong SpanId = 2;
        private const SamplingPriority SamplingPriority = Trace.SamplingPriority.UserReject;
        private const string Origin = "origin";
        private const string DatadogTags = "key1=value1;key2=value2";

        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        private static readonly KeyValuePair<string, string>[] DefaultHeaderValues =
        {
            new(HttpHeaderNames.TraceId, TraceId.ToString(InvariantCulture)),
            new(HttpHeaderNames.ParentId, SpanId.ToString(InvariantCulture)),
            new(HttpHeaderNames.SamplingPriority, ((int)SamplingPriority).ToString(InvariantCulture)),
            new(HttpHeaderNames.Origin, Origin),
            new(HttpHeaderNames.DatadogTags, DatadogTags),
        };

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
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { DatadogTags = DatadogTags };
            var headers = new Mock<IHeadersCollection>();

            SpanContextPropagator.Instance.Inject(context, headers.Object);

            VerifySetCalls(headers);
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin) { DatadogTags = DatadogTags };

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            SpanContextPropagator.Instance.Inject(context, headers.Object, &Setter);

            VerifySetCalls(headers);

            static void Setter(IHeadersCollection carrier, string name, string value)
                => carrier.Set(name, value);
        }

        [Fact]
        public void Inject_TraceIdSpanIdOnly()
        {
            var context = new SpanContext(TraceId, SpanId, samplingPriority: null, serviceName: null, origin: null) { DatadogTags = null };
            var headers = new Mock<IHeadersCollection>();

            SpanContextPropagator.Instance.Inject(context, headers.Object);

            // null values are not set, so only traceId and spanId (the first two in the list) should be set
            headers.Verify(h => h.Set(HttpHeaderNames.TraceId, TraceId.ToString(InvariantCulture)), Times.Once());
            headers.Verify(h => h.Set(HttpHeaderNames.ParentId, SpanId.ToString(InvariantCulture)), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        [Fact]
        public void Extract_IHeadersCollection()
        {
            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            var headers = SetupMockHeadersCollection();
            object result = SpanContextPropagator.Instance.Extract(headers.Object);

            VerifyGetCalls(headers);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId = TraceId,
                           SpanId = SpanId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority
                       });
        }

        [Fact]
        public void Extract_CarrierAndDelegate()
        {
            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = SetupMockHeadersCollection();

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            object result = SpanContextPropagator.Instance.Extract(headers.Object, &Getter);

            VerifyGetCalls(headers);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId = TraceId,
                           SpanId = SpanId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority
                       });

            static StringEnumerable Getter(IHeadersCollection carrier, string name)
                => carrier.GetValues(name);
        }

        [Fact]
        public void Extract_ReadOnlyDictionary()
        {
            var headers = SetupMockReadOnlyDictionary();

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            object result = SpanContextPropagator.Instance.Extract(headers.Object);

            VerifyGetCalls(headers);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId = TraceId,
                           SpanId = SpanId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority
                       });
        }

        [Fact]
        public void Extract_EmptyHeadersReturnsNull()
        {
            var headers = new Mock<IHeadersCollection>();

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            object result = SpanContextPropagator.Instance.Extract(headers.Object);

            result.Should().BeNull();
        }

        [Fact]
        public void Extract_TraceIdOnly()
        {
            var headers = new Mock<IHeadersCollection>();

            // only setup TraceId, other properties remain null/empty
            headers.Setup(h => h.GetValues(HttpHeaderNames.TraceId)).Returns(new StringEnumerable(TraceId.ToString(InvariantCulture)));

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            object result = SpanContextPropagator.Instance.Extract(headers.Object);

            result.Should().BeEquivalentTo(new SpanContextMock { TraceId = TraceId });
        }

        [Fact]
        public void SpanContextRoundTrip()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin);

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            object result = SpanContextPropagator.Instance.Extract(context);

            result.Should().NotBeSameAs(context);
            result.Should().BeEquivalentTo(context);
        }

        [Fact]
        public void Identity()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin);
            var headers = new NameValueHeadersCollection(new NameValueCollection());

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            SpanContextPropagator.Instance.Inject(context, headers);
            object result = SpanContextPropagator.Instance.Extract(headers);

            result.Should().NotBeSameAs(context);
            result.Should().BeEquivalentTo(context);
        }

        [Theory]
        [MemberData(nameof(GetInvalidIds))]
        public void Extract_InvalidTraceId(string traceId)
        {
            var headers = SetupMockHeadersCollection();

            // replace TraceId setup
            headers.Setup(h => h.GetValues(HttpHeaderNames.TraceId)).Returns(new StringEnumerable(traceId));

            var result = SpanContextPropagator.Instance.Extract(headers.Object);

            // invalid traceId should return a null context even if other values are set
            result.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(GetInvalidIds))]
        public void Extract_InvalidSpanId(string spanId)
        {
            var headers = SetupMockHeadersCollection();

            // replace ParentId setup
            headers.Setup(h => h.GetValues(HttpHeaderNames.ParentId)).Returns(new StringEnumerable(spanId));

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            object result = SpanContextPropagator.Instance.Extract(headers.Object);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           // SpanId has default value
                           TraceId = TraceId,
                           Origin = Origin,
                           SamplingPriority = SamplingPriority
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
            headers.Setup(h => h.GetValues(HttpHeaderNames.SamplingPriority)).Returns(new StringEnumerable(samplingPriority));

            object result = SpanContextPropagator.Instance.Extract(headers.Object);

            result.Should()
                  .BeEquivalentTo(
                       new SpanContextMock
                       {
                           TraceId = TraceId,
                           SpanId = SpanId,
                           Origin = Origin,
                           SamplingPriority = (SamplingPriority?)expectedSamplingPriority
                       });
        }

        private static Mock<IHeadersCollection> SetupMockHeadersCollection()
        {
            var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

            foreach (var pair in DefaultHeaderValues)
            {
                headers.Setup(h => h.GetValues(pair.Key)).Returns(new StringEnumerable(pair.Value));
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

        private static void VerifySetCalls(Mock<IHeadersCollection> headers)
        {
            var once = Times.Once();

            foreach (var pair in DefaultHeaderValues)
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

        public string Origin { get; set; }

        public SamplingPriority? SamplingPriority { get; set; }

        public ISpanContext Parent { get; set; }

        public ulong? ParentId { get; set; }

        public string ServiceName { get; set; }

        public TraceContext TraceContext { get; set; }
    }
#pragma warning restore SA1402 // File may only contain a single type
}
