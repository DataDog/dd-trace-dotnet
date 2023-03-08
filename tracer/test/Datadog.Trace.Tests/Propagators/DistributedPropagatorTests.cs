// <copyright file="DistributedPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators;

public class DistributedPropagatorTests
{
    private const ulong SpanId = 2;
    private const int SamplingPriority = SamplingPriorityValues.UserReject;
    private const string Origin = "origin";
    private const string RawTraceId = "1a";
    private const string RawSpanId = "2b";
    private const string PropagatedTagsString = "_dd.p.key1=value1,_dd.p.key2=value2";
    private const string AdditionalW3CTraceState = "key3=value3,key4=value4";
    private static readonly TraceId TraceId = (TraceId)1;

    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    private static readonly SpanContextPropagator Propagator;

    private static readonly KeyValuePair<string, string>[] DefaultHeaderValues =
    {
        new("__DistributedKey-TraceId", TraceId.Lower.ToString(InvariantCulture)),
        new("__DistributedKey-ParentId", SpanId.ToString(InvariantCulture)),
        new("__DistributedKey-SamplingPriority", SamplingPriority.ToString(InvariantCulture)),
        new("__DistributedKey-Origin", Origin),
        new("__DistributedKey-RawTraceId", RawTraceId),
        new("__DistributedKey-RawSpanId", RawSpanId),
        new("__DistributedKey-PropagatedTags", PropagatedTagsString),
        new("__DistributedKey-AdditionalW3CTraceState", AdditionalW3CTraceState),
    };

    private static readonly TraceTagCollection PropagatedTagsCollection = new(
        TagPropagation.OutgoingTagPropagationHeaderMaxLength,
        new List<KeyValuePair<string, string>>
        {
            new("_dd.p.key1", "value1"),
            new("_dd.p.key2", "value2"),
        },
        PropagatedTagsString);

    private static readonly TraceTagCollection EmptyPropagatedTags = new(TagPropagation.OutgoingTagPropagationHeaderMaxLength);

    static DistributedPropagatorTests()
    {
        Propagator = new SpanContextPropagator(
            injectors: null,
            extractors: new IContextExtractor[] { DistributedContextExtractor.Instance });
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
    public void Extract_ReadOnlyDictionary()
    {
        // set up all key/value pairs
        var headers = SetupMockReadOnlyDictionary();

        // extract SpanContext
        var result = Propagator.Extract(headers.Object);

        VerifyGetCalls(headers);

        result.Should()
              .BeEquivalentTo(
                   new SpanContextMock
                   {
                       TraceId = TraceId,
                       SpanId = SpanId,
                       RawTraceId = RawTraceId,
                       RawSpanId = RawSpanId,
                       Origin = Origin,
                       SamplingPriority = SamplingPriority,
                       PropagatedTags = PropagatedTagsCollection,
                       AdditionalW3CTraceState = AdditionalW3CTraceState,
                   });
    }

    [Fact]
    public void Extract_EmptyHeadersReturnsNull()
    {
        // empty
        var headers = new Mock<IReadOnlyDictionary<string, string>>();

        // extract SpanContext
        var result = Propagator.Extract(headers.Object);

        result.Should().BeNull();
    }

    [Fact]
    public void Extract_TraceIdOnly()
    {
        var value = TraceId.Lower.ToString(InvariantCulture);

        // only setup TraceId, other properties remain null/empty
        var headers = new Mock<IReadOnlyDictionary<string, string>>();
        headers.Setup(h => h.TryGetValue("__DistributedKey-TraceId", out value)).Returns(true);

        // extract SpanContext
        var result = Propagator.Extract(headers.Object);

        result.Should().BeEquivalentTo(new SpanContextMock
                                       {
                                           TraceId = TraceId,
                                           PropagatedTags = EmptyPropagatedTags,
                                       });
    }

    [Fact]
    public void SpanContextRoundTrip()
    {
        var propagatedTags = new TraceTagCollection(100);
        propagatedTags.SetTag("_dd.p.key1", "value1");
        propagatedTags.SetTag("_dd.p.key2", "value2");

        var traceContext = new TraceContext(tracer: null, propagatedTags);
        traceContext.SetSamplingPriority(SamplingPriority);
        traceContext.Origin = Origin;
        traceContext.AdditionalW3CTraceState = AdditionalW3CTraceState;

        // create and populate SpanContext
        IReadOnlyDictionary<string, string> context = new SpanContext(
            parent: SpanContext.None,
            traceContext,
            serviceName: null,
            TraceId,
            SpanId,
            RawTraceId,
            RawSpanId);

        // extract SpanContext
        var result = Propagator.Extract(context);

        // they are not the same SpanContext instance...
        result.Should().NotBeSameAs(context);

        // ...but they contain the same values
        result.Should().BeEquivalentTo(context);
    }

    [Theory]
    [MemberData(nameof(GetInvalidIds))]
    public void Extract_InvalidTraceId(string traceId)
    {
        // set up all key/value pairs
        var headers = SetupMockReadOnlyDictionary();

        // replace TraceId setup
        var value = traceId;
        headers.Setup(h => h.TryGetValue("__DistributedKey-TraceId", out value)).Returns(true);

        // extract SpanContext
        var result = Propagator.Extract(headers.Object);

        // invalid traceId should return a null context even if other values are set
        result.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(GetInvalidIds))]
    public void Extract_InvalidSpanId(string spanId)
    {
        // set up all key/value pairs
        var headers = SetupMockReadOnlyDictionary();

        // replace ParentId setup
        var value = spanId;
        headers.Setup(h => h.TryGetValue("__DistributedKey-ParentId", out value)).Returns(true);

        // extract SpanContext
        var result = Propagator.Extract(headers.Object);

        result.Should()
              .BeEquivalentTo(
                   new SpanContextMock
                   {
                       // SpanId has default value
                       TraceId = TraceId,
                       Origin = Origin,
                       RawTraceId = RawTraceId,
                       RawSpanId = RawSpanId,
                       SamplingPriority = SamplingPriority,
                       PropagatedTags = PropagatedTagsCollection,
                       AdditionalW3CTraceState = AdditionalW3CTraceState
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
        // even if we don't recognize its value (to allow forward compatibility with newly added values).
        // ignore the extracted sampling priority if it is not a valid integer.

        // set up all key/value pairs
        var headers = SetupMockReadOnlyDictionary();

        // replace SamplingPriority setup
        var value = samplingPriority;
        headers.Setup(h => h.TryGetValue("__DistributedKey-SamplingPriority", out value)).Returns(true);

        // extract SpanContext
        object result = Propagator.Extract(headers.Object);

        result.Should()
              .BeEquivalentTo(
                   new SpanContextMock
                   {
                       TraceId = TraceId,
                       SpanId = SpanId,
                       RawTraceId = RawTraceId,
                       RawSpanId = RawSpanId,
                       Origin = Origin,
                       SamplingPriority = expectedSamplingPriority,
                       PropagatedTags = PropagatedTagsCollection,
                       AdditionalW3CTraceState = AdditionalW3CTraceState,
                   });
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
