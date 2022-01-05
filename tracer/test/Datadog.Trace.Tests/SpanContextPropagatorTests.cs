// <copyright file="SpanContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
            var headers = new Mock<IHeadersCollection>();

            SpanContextPropagator.Instance.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

            VerifySetCalls(headers);
        }

        [Fact]
        public void Extract_IHeadersCollection()
        {
            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            var headers = CreatePopulatedHeaders();
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
            // use `object` so Should() below works correctly,
            // otherwise it picks up the IEnumerable<KeyValuePair<string, string>> overload
            var headers = CreatePopulatedHeaders();
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


        }
    }
}
