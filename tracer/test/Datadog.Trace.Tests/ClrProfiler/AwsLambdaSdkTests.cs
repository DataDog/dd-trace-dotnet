// <copyright file="AwsLambdaSdkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.LambdaSdk;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler;

public class AwsLambdaSdkTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("RequestResponse")]
    public async Task CreatesChildScopeAndInjectsItsContext(string invocationType)
    {
        var settings = TracerSettings.Create(new()
        {
            { ConfigurationKeys.PropagationStyleInject, "datadog,tracecontext" },
            { ConfigurationKeys.PropagationStyleExtract, "datadog,tracecontext" },
        });
        await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
        var request = CreateRequest(invocationType, null);
        using var parent = tracer.StartActiveInternal("parent");
        using var scope = AwsLambdaSdkCommon.CreateScope(tracer, request.DuckCast<IInvokeRequest>());

        scope.Should().NotBeNull();
        scope.Span.Context.ParentId.Should().Be(parent.Span.SpanId);
        var context = Decode(request.ClientContextBase64);
        var headers = context["custom"].ToObject<Dictionary<string, string>>();
        var extracted = tracer.TracerManager.SpanContextPropagator.Extract(headers).SpanContext;
        extracted.Should().NotBeNull();
        extracted.TraceId128.Should().Be(scope.Span.TraceId128);
        extracted.SpanId.Should().Be(scope.Span.SpanId);
    }

    [Theory]
    [InlineData("Event")]
    [InlineData("DryRun")]
    public async Task SkipsOtherInvocationTypes(string invocationType)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        var request = CreateRequest(invocationType, "{\"custom\":{\"application\":\"value\"}}");
        var original = request.ClientContextBase64;

        AwsLambdaSdkCommon.CreateScope(tracer, request.DuckCast<IInvokeRequest>()).Should().BeNull();
        request.ClientContextBase64.Should().Be(original);
    }

    [Theory]
    [InlineData("DD_TRACE_ENABLED")]
    [InlineData("DD_TRACE_AWSSDK_ENABLED")]
    [InlineData("DD_TRACE_AWSLAMBDASDK_ENABLED")]
    public async Task RespectsDisabledIntegrations(string key)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent(TracerSettings.Create(new() { { key, false } }));
        var request = CreateRequest("RequestResponse", null);

        AwsLambdaSdkCommon.CreateScope(tracer, request.DuckCast<IInvokeRequest>()).Should().BeNull();
        request.ClientContextBase64.Should().BeNull();
    }

    [Fact]
    public async Task DoesNotDependOnLambdaHandlerInstrumentation()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent(TracerSettings.Create(new() { { "DD_TRACE_AWSLAMBDA_ENABLED", false } }));
        var request = CreateRequest(null, null);
        using var scope = AwsLambdaSdkCommon.CreateScope(tracer, request.DuckCast<IInvokeRequest>());

        scope.Should().NotBeNull();
        Decode(request.ClientContextBase64)["custom"]["x-datadog-parent-id"].Should().NotBeNull();
    }

    private static InvokeRequestData CreateRequest(string invocationType, string json) => new()
    {
        FunctionName = "example",
        InvocationType = invocationType is null ? null : new InvocationTypeData { Value = invocationType },
        ClientContextBase64 = json is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
    };

    private static JObject Decode(string encoded) => JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));

    public class InvokeRequestData
    {
        public string FunctionName { get; set; }

        public InvocationTypeData InvocationType { get; set; }

        public string ClientContextBase64 { get; set; }
    }

    public class InvocationTypeData
    {
        public string Value { get; set; }
    }
}
