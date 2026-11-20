// <copyright file="ObjectExtractorTruncationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
#if NETCOREAPP
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
#endif
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

// ObjectExtractor cuts the containers and the depth of a body before the encoder sees it, so the encoder has to
// learn what it cut to report the truncation
public class ObjectExtractorTruncationTests : WafLibraryRequiredTest
{
    [SkippableTheory]
    [CombinatorialData]
    public void GivenAMapLargerThanTheLimit_WhenExtractedAndEncoded_ThenItsOriginalSizeIsRecorded(bool useUnsafeEncoder)
    {
        var body = Enumerable.Range(0, 300).ToDictionary(i => $"key{i}", i => (object)$"value{i}");

        var truncation = ExtractAndEncode(useUnsafeEncoder, body);

        truncation.ContainerSize.Should().Be(300);
        truncation.Reason.Should().Be(MetricTags.TruncationReason.ListOrMapTooLarge);
    }

    [SkippableTheory]
    [CombinatorialData]
    public void GivenAListLargerThanTheLimit_WhenExtractedAndEncoded_ThenItsOriginalSizeIsRecorded(bool useUnsafeEncoder)
    {
        var body = new { Items = Enumerable.Range(0, 300).Select(i => $"value{i}").ToList() };

        var truncation = ExtractAndEncode(useUnsafeEncoder, body);

        truncation.ContainerSize.Should().Be(300);
        truncation.Reason.Should().Be(MetricTags.TruncationReason.ListOrMapTooLarge);
    }

    [SkippableTheory]
    [CombinatorialData]
    public void GivenAnObjectDeeperThanTheLimit_WhenExtractedAndEncoded_ThenTheDepthIsRecorded(bool useUnsafeEncoder)
    {
        var truncation = ExtractAndEncode(useUnsafeEncoder, MakeNestedMap(25));

        truncation.ContainerDepth.Should().BeGreaterThan(0);
        truncation.Reason.Should().Be(MetricTags.TruncationReason.ObjectTooDeep);
    }

    [SkippableTheory]
    [CombinatorialData]
    public void GivenContainersAtTheLimit_WhenExtractedAndEncoded_ThenNothingIsRecorded(bool useUnsafeEncoder)
    {
        var body = new
        {
            Map = Enumerable.Range(0, WafConstants.MaxContainerSize).ToDictionary(i => $"key{i}", i => (object)$"value{i}"),
            List = Enumerable.Range(0, WafConstants.MaxContainerSize).Select(i => $"value{i}").ToList(),
        };

        var truncation = ExtractAndEncode(useUnsafeEncoder, body);

        truncation.Truncated.Should().BeFalse();
    }

#if NETCOREAPP
    [SkippableFact]
    public void GivenARequestBodyLargerThanTheLimits_WhenChecked_ThenTheTruncationIsSetOnTheWebSpan()
    {
        var httpContext = new Mock<HttpContext>();
        var httpResponse = new Mock<HttpResponse>();
        httpResponse.Setup(x => x.StatusCode).Returns(200);
        httpContext.Setup(x => x.Response).Returns(httpResponse.Object);
        var routing = new Mock<IRoutingFeature>();
        routing.Setup(x => x.RouteData).Returns(new RouteData());
        httpContext.Setup(x => x.Features[typeof(IRoutingFeature)]).Returns(routing.Object);
        var traceContext = new TraceContext(new EmptyDatadogTracer());
        var span = new Span(new SpanContext(parent: null, traceContext, serviceName: "My Service Name", traceId: (TraceId)100, spanId: 200), DateTimeOffset.Now);
        var waf = CreateWaf().Waf;
        waf.Should().NotBeNull();
        var security = new AppSec.Security(waf: waf);
        var body = new
        {
            LargeObject = Enumerable.Range(0, 300).ToDictionary(i => $"key{i}", i => (object)$"value{i}"),
            DeepObject = MakeNestedMap(25),
        };

        security.CheckBody(httpContext.Object, span, body, response: false);
        traceContext.AppSecRequestContext.CloseWebSpan(span);

        span.GetMetric(Metrics.AppSecTruncatedContainerSize).Should().Be(300);
        span.GetMetric(Metrics.AppSecTruncatedContainerDepth).Should().BeGreaterThan(0);
    }
#endif

    private static Dictionary<string, object> MakeNestedMap(int nestingDepth)
    {
        var root = new Dictionary<string, object> { { "value", "a" } };
        for (var i = 0; i < nestingDepth; i++)
        {
            root = new Dictionary<string, object> { { "a", root } };
        }

        return root;
    }

    private static TruncationInfo ExtractAndEncode(bool useUnsafeEncoder, object body)
    {
        IEncoder encoder = useUnsafeEncoder ? new Encoder() : new EncoderLegacy(WafLibraryInvoker!);
        var extracted = ObjectExtractor.Extract(body);
        extracted.Should().NotBeNull();

        using var result = encoder.Encode(new Dictionary<string, object> { { AddressesConstants.RequestBody, extracted! } }, applySafetyLimits: true);
        return result.Truncation;
    }
}
