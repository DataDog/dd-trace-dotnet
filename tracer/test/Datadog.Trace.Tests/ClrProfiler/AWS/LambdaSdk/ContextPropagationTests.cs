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
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AWS.LambdaSdk;

[Collection(nameof(BaggageTestCollection))]
[BaggageRestorer]
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

    [Theory]
    [InlineData("datadog,baggage")]
    [InlineData("datadog,tracecontext,baggage")]
    [InlineData("datadog")]
    [InlineData("baggage")]
    [InlineData("tracecontext")]
    [InlineData("none")]
    public async Task ReusingARequestClearsOptionalFieldsOnlyForEnabledInjectors(string propagationStyle)
    {
        var settings = TracerSettings.Create(new()
        {
            { ConfigurationKeys.PropagationStyleInject, propagationStyle },
        });
        await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
        var request = CreateRequest("{\"custom\":{\"application\":\"value\",\"x-datadog-origin\":\"application-origin\",\"x-datadog-sampling-priority\":\"2\",\"x-datadog-tags\":\"_dd.p.application=value\",\"baggage\":\"application=value\"},\"env\":{\"name\":\"example\"}}");
        var first = new SpanContext((TraceId)1, 2, samplingPriority: 1, serviceName: null, origin: "synthetics")
        {
            PropagatedTags = new TraceTagCollection(),
        };
        first.PropagatedTags.SetTag("_dd.p.previous", "value");
        Baggage.Current = new Baggage { { "previous", "value" } };

        ContextPropagation.Inject(tracer, request.DuckCast<IInvokeRequest>(), first);

        var firstCustom = Decode(request.ClientContextBase64)["custom"];
        var injectsDatadog = propagationStyle.Contains("datadog");
        var injectsBaggage = propagationStyle.Contains("baggage");
        firstCustom[HttpHeaderNames.Origin].Value<string>().Should().Be(injectsDatadog ? "synthetics" : "application-origin");
        firstCustom[HttpHeaderNames.SamplingPriority].Value<string>().Should().Be(injectsDatadog ? "1" : "2");
        firstCustom[HttpHeaderNames.PropagatedTags].Value<string>().Should().Be(injectsDatadog ? "_dd.p.previous=value" : "_dd.p.application=value");
        firstCustom["baggage"].Value<string>().Should().Be(injectsBaggage ? "previous=value" : "application=value");

        Baggage.Current.Clear();
        var second = new SpanContext((TraceId)3, 4, samplingPriority: null, serviceName: null, origin: null);
        ContextPropagation.Inject(tracer, request.DuckCast<IInvokeRequest>(), second);

        var context = Decode(request.ClientContextBase64);
        var custom = context["custom"].ToObject<Dictionary<string, string>>();
        if (injectsDatadog)
        {
            custom.Should().NotContainKey(HttpHeaderNames.Origin)
                  .And.NotContainKey(HttpHeaderNames.SamplingPriority)
                  .And.NotContainKey(HttpHeaderNames.PropagatedTags);
            custom[HttpHeaderNames.TraceId].Should().Be("3");
            custom[HttpHeaderNames.ParentId].Should().Be("4");
        }
        else
        {
            custom[HttpHeaderNames.Origin].Should().Be("application-origin");
            custom[HttpHeaderNames.SamplingPriority].Should().Be("2");
            custom[HttpHeaderNames.PropagatedTags].Should().Be("_dd.p.application=value");
        }

        if (injectsBaggage)
        {
            custom.Should().NotContainKey("baggage");
        }
        else
        {
            custom["baggage"].Should().Be("application=value");
        }

        custom["application"].Should().Be("value");
        context["env"]["name"].Value<string>().Should().Be("example");
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
