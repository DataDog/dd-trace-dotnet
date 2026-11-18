// <copyright file="ContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.LambdaSdk;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AWS.LambdaSdk;

public class ContextPropagationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"custom\":null}")]
    [InlineData("{\"custom\":{\"application\":\"value\",\"timestamp\":\"2026-11-18T00:00:00Z\"},\"env\":{\"name\":\"example\"}}")]
    public async Task InjectsConfiguredPropagationFormatsAndPreservesFields(string originalJson)
    {
        var settings = TracerSettings.Create(new()
        {
            { ConfigurationKeys.PropagationStyleInject, "datadog,tracecontext" },
            { ConfigurationKeys.PropagationStyleExtract, "datadog,tracecontext" },
        });
        await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
        using var scope = tracer.StartActiveInternal("caller");
        var request = CreateRequest(originalJson);

        ContextPropagation.Inject(tracer, request.DuckCast<IInvokeRequest>(), scope.Span.Context);

        var context = Decode(request.ClientContextBase64);
        var headers = context["custom"].ToObject<Dictionary<string, string>>();
        var extracted = tracer.TracerManager.SpanContextPropagator.Extract(headers).SpanContext;
        extracted.Should().NotBeNull();
        extracted.TraceId128.Should().Be(scope.Span.TraceId128);
        extracted.SpanId.Should().Be(scope.Span.SpanId);
        headers.Should().ContainKey("traceparent");
        if (originalJson?.Contains("application") == true)
        {
            context["custom"]["application"].Value<string>().Should().Be("value");
            Encoding.UTF8.GetString(Convert.FromBase64String(request.ClientContextBase64)).Should().Contain("\"timestamp\":\"2026-11-18T00:00:00Z\"");
            context["env"]["name"].Value<string>().Should().Be("example");
        }
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("[]")]
    [InlineData("{invalid}")]
    [InlineData("{}{}")]
    [InlineData("{\"custom\":[]}")]
    [InlineData("{\"custom\":\"application data\"}")]
    public async Task PreservesInvalidClientContext(string input)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        using var scope = tracer.StartActiveInternal("caller");
        var request = CreateRequest(input);
        if (input == "not base64!")
        {
            request.ClientContextBase64 = input;
        }

        var original = request.ClientContextBase64;
        ContextPropagation.Inject(tracer, request.DuckCast<IInvokeRequest>(), scope.Span.Context);

        request.ClientContextBase64.Should().Be(original);
    }

    [Theory]
    [InlineData(2550)] // Fits before injection, exceeds the limit after injection.
    [InlineData(3584)] // Already too large before injection.
    public async Task DoesNotExceedTheEncodedSizeLimit(int valueLength)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        using var scope = tracer.StartActiveInternal("caller");
        var request = CreateRequest("{\"custom\":{\"application\":\"" + new string('x', valueLength) + "\"}}");
        var original = request.ClientContextBase64;

        ContextPropagation.Inject(tracer, request.DuckCast<IInvokeRequest>(), scope.Span.Context);

        request.ClientContextBase64.Should().Be(original);
    }

    [Fact]
    public async Task ReusingARequestOverwritesThePreviousSpanContext()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        var request = CreateRequest(null);
        string previous;
        using (var first = tracer.StartActiveInternal("first"))
        {
            ContextPropagation.Inject(tracer, request.DuckCast<IInvokeRequest>(), first.Span.Context);
            previous = request.ClientContextBase64;
        }

        using var second = tracer.StartActiveInternal("second");
        ContextPropagation.Inject(tracer, request.DuckCast<IInvokeRequest>(), second.Span.Context);

        request.ClientContextBase64.Should().NotBe(previous);
        Decode(request.ClientContextBase64)["custom"]["x-datadog-parent-id"].Value<string>().Should().Be(second.Span.SpanId.ToString(CultureInfo.InvariantCulture));
    }

    private static ClientContextCarrier CreateRequest(string json) => new()
    {
        ClientContextBase64 = json is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
    };

    private static JObject Decode(string encoded) => JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));

    private class ClientContextCarrier
    {
        public string FunctionName => "example";

        public IInvocationType InvocationType => null;

        public string ClientContextBase64 { get; set; }
    }
}
