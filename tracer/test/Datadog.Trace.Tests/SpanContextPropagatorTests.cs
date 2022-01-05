// <copyright file="SpanContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Datadog.Trace.Headers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class SpanContextPropagatorTests
    {
        private const ulong TraceId = 1;
        private const ulong SpanId = 2;
        private const SamplingPriority SamplingPriority = Trace.SamplingPriority.UserReject;
        private const string Origin = "origin";

        public static IEnumerable<object[]> GetInvalidIds()
        {
            yield return new object[] { null };
            yield return new object[] { string.Empty };
            yield return new object[] { "0" };
            yield return new object[] { "-1" };
            yield return new object[] { "id" };
        }

        [Fact]
        public void Inject_IHeadersCollection()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin);
            var headers = new Mock<IHeadersCollection>();

            SpanContextPropagator.Instance.Inject(context, headers.Object);

            VerifySetCalls(headers);
        }

        [Fact]
        public void Inject_CarrierAndDelegate()
        {
            var context = new SpanContext(TraceId, SpanId, SamplingPriority, serviceName: null, Origin);

            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = new Mock<IHeadersCollection>();

            SpanContextPropagator.Instance.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            VerifySetCalls(headers);
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
                       new
                       {
                           TraceId,
                           SpanId,
                           Origin,
                           SamplingPriority
                       });
        }

        [Fact]
        public void Extract_CarrierAndDelegate()
        {
            // using IHeadersCollection for convenience, but carrier could be any type
            var headers = SetupMockHeadersCollection();

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            object result = SpanContextPropagator.Instance.Extract(headers.Object, (carrier, name) => carrier.GetValues(name));

            VerifyGetCalls(headers);

            result.Should()
                  .BeEquivalentTo(
                       new
                       {
                           TraceId,
                           SpanId,
                           Origin,
                           SamplingPriority
                       });
        }

        [Fact]
        public void Extract_EmptyHeadersReturnsNull()
        {
            var headers = new Mock<IHeadersCollection>();

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            object resultContext = SpanContextPropagator.Instance.Extract(headers.Object);

            resultContext.Should().BeNull();
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
            headers.Setup(h => h.GetValues(HttpHeaderNames.TraceId)).Returns(new[] { traceId });

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
            headers.Setup(h => h.GetValues(HttpHeaderNames.ParentId)).Returns(new[] { spanId });

            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            object result = SpanContextPropagator.Instance.Extract(headers.Object);

            result.Should()
                  .BeEquivalentTo(
                       new
                       {
                           TraceId,
                           SpanId = 0,
                           Origin,
                           SamplingPriority
                       });
        }

        [Theory]
        [InlineData(-1000)]
        [InlineData(1000)]
        public void Extract_InvalidIntegerSamplingPriority(int samplingPriority)
        {
            // if the extracted sampling priority is a valid integer, pass it along as-is,
            // even if we don't recognize its value to allow forward compatibility with newly added values.

            var headers = SetupMockHeadersCollection();

            // replace SamplingPriority setup
            headers.Setup(h => h.GetValues(HttpHeaderNames.SamplingPriority)).Returns(new[] { samplingPriority.ToString(CultureInfo.InvariantCulture) });

            object result = SpanContextPropagator.Instance.Extract(headers.Object);

            result.Should()
                  .BeEquivalentTo(
                       new
                       {
                           TraceId,
                           SpanId,
                           Origin,
                           SamplingPriority = samplingPriority
                       });
        }

        [Theory]
        [InlineData("1.0")]
        [InlineData("1,0")]
        [InlineData("sampling.priority")]
        public void Extract_InvalidNonIntegerSamplingPriority(string samplingPriority)
        {
            // ignore the extracted sampling priority if it is not a valid integer

            var headers = SetupMockHeadersCollection();

            // replace SamplingPriority setup
            headers.Setup(h => h.GetValues(HttpHeaderNames.SamplingPriority)).Returns(new[] { samplingPriority });

            object result = SpanContextPropagator.Instance.Extract(headers.Object);

            result.Should()
                  .BeEquivalentTo(
                       new
                       {
                           TraceId,
                           SpanId,
                           Origin,
                           SamplingPriority = default(SamplingPriority?)
                       });
        }

        private static Mock<IHeadersCollection> SetupMockHeadersCollection()
        {
            return SetupMockHeadersCollection(TraceId.ToString(), SpanId.ToString());
        }

        private static Mock<IHeadersCollection> SetupMockHeadersCollection(string traceId, string spanId)
        {
            var headers = new Mock<IHeadersCollection>();
            headers.Setup(h => h.GetValues(HttpHeaderNames.TraceId)).Returns(new[] { traceId });
            headers.Setup(h => h.GetValues(HttpHeaderNames.ParentId)).Returns(new[] { spanId });
            headers.Setup(h => h.GetValues(HttpHeaderNames.SamplingPriority)).Returns(new[] { ((int)SamplingPriority).ToString() });
            headers.Setup(h => h.GetValues(HttpHeaderNames.Origin)).Returns(new[] { Origin });
            return headers;
        }

        private static void VerifySetCalls(Mock<IHeadersCollection> headers)
        {
            headers.Verify(h => h.Set(HttpHeaderNames.TraceId, TraceId.ToString()), Times.Once());
            headers.Verify(h => h.Set(HttpHeaderNames.ParentId, SpanId.ToString()), Times.Once());
            headers.Verify(h => h.Set(HttpHeaderNames.SamplingPriority, ((int)SamplingPriority).ToString()), Times.Once());
            headers.Verify(h => h.Set(HttpHeaderNames.Origin, Origin), Times.Once());
            headers.VerifyNoOtherCalls();
        }

        private static void VerifyGetCalls(Mock<IHeadersCollection> headers)
        {
            headers.Verify(h => h.GetValues(HttpHeaderNames.TraceId), Times.Once());
            headers.Verify(h => h.GetValues(HttpHeaderNames.ParentId), Times.Once());
            headers.Verify(h => h.GetValues(HttpHeaderNames.SamplingPriority), Times.Once());
            headers.Verify(h => h.GetValues(HttpHeaderNames.Origin), Times.Once());
            headers.VerifyNoOtherCalls();
        }
    }
}
