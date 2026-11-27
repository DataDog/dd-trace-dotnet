// <copyright file="ObjectExtractorTruncationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
#if NETFRAMEWORK
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
#endif
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

#if NETFRAMEWORK
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenADataContractWithMoreMembersThanTheLimit_WhenExtracted_ThenOnlyTheEmittedMembersCount(bool lastMemberEmitted)
    {
        // the last member is [DataMember(EmitDefaultValue = false)], so the serializer omits it when it's null
        var fields = CreateDataContractType(WafConstants.MaxContainerSize + 1).GetFields();
        var instance = Activator.CreateInstance(fields[0].DeclaringType!);
        for (var i = 0; i < fields.Length - 1; i++)
        {
            fields[i].SetValue(instance, "value");
        }

        fields[fields.Length - 1].SetValue(instance, lastMemberEmitted ? "value" : null);

        var extracted = ObjectExtractor.ExtractDataContract(instance);

        extracted.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Which.Should().HaveCount(WafConstants.MaxContainerSize);
        if (lastMemberEmitted)
        {
            extracted.Should().BeAssignableTo<ITruncatedContainer>().Which.OriginalCount.Should().Be(WafConstants.MaxContainerSize + 1);
        }
        else
        {
            extracted.Should().NotBeAssignableTo<ITruncatedContainer>();
        }
    }

    private static Type CreateDataContractType(int memberCount)
    {
        var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ObjectExtractorTruncationTests.DataContracts"), AssemblyBuilderAccess.Run)
                                    .DefineDynamicModule("DataContracts");
        var type = module.DefineType("WideDataContract" + memberCount, TypeAttributes.Public | TypeAttributes.Class);
        type.SetCustomAttribute(new CustomAttributeBuilder(typeof(DataContractAttribute).GetConstructor(Type.EmptyTypes)!, []));

        var dataMember = typeof(DataMemberAttribute).GetConstructor(Type.EmptyTypes)!;
        var emitDefaultValue = typeof(DataMemberAttribute).GetProperty(nameof(DataMemberAttribute.EmitDefaultValue))!;
        for (var i = 0; i < memberCount; i++)
        {
            var field = type.DefineField($"Member{i}", typeof(string), FieldAttributes.Public);
            field.SetCustomAttribute(
                i == memberCount - 1
                    ? new CustomAttributeBuilder(dataMember, [], [emitDefaultValue], [false])
                    : new CustomAttributeBuilder(dataMember, []));
        }

        return type.CreateType()!;
    }
#endif

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
