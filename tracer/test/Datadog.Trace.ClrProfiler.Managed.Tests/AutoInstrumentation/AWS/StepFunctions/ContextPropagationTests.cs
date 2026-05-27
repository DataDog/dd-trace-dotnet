// <copyright file="ContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.StepFunctions.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.StepFunctions;

public class ContextPropagationTests
{
    private const string DatadogKey = "_datadog";

    [Fact]
    public async Task InjectContextIntoInput_EmptyInput_ProducesParseableJson()
    {
        var spanContext = CreateSpanContext(origin: null);
        var request = new StartExecutionRequest { Input = "{}" };
        var proxy = request.DuckCast<StartExecutionIntegration.IStartExecutionRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContextIntoInput<object, StartExecutionIntegration.IStartExecutionRequest>(
            tracer, proxy, new PropagationContext(spanContext, baggage: null));

        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(proxy.Input!)!;
        parsed.Should().ContainKey(DatadogKey);

        var datadog = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(parsed[DatadogKey]))!;
        datadog["x-datadog-trace-id"].Should().Be(spanContext.TraceId.ToString());
        datadog["x-datadog-parent-id"].Should().Be(spanContext.SpanId.ToString());
    }

    [Fact]
    public async Task InjectContextIntoInput_NonEmptyInput_PreservesExistingKeys()
    {
        var spanContext = CreateSpanContext(origin: null);
        var request = new StartExecutionRequest { Input = """{"foo":"bar"}""" };
        var proxy = request.DuckCast<StartExecutionIntegration.IStartExecutionRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContextIntoInput<object, StartExecutionIntegration.IStartExecutionRequest>(
            tracer, proxy, new PropagationContext(spanContext, baggage: null));

        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(proxy.Input!)!;
        parsed.Should().ContainKey("foo");
        parsed["foo"].Should().Be("bar");
        parsed.Should().ContainKey(DatadogKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-json")]
    public async Task InjectContextIntoInput_InvalidInput_DoesNothing(string? input)
    {
        var spanContext = CreateSpanContext(origin: null);
        var request = new StartExecutionRequest { Input = input };
        var proxy = request.DuckCast<StartExecutionIntegration.IStartExecutionRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContextIntoInput<object, StartExecutionIntegration.IStartExecutionRequest>(
            tracer, proxy, new PropagationContext(spanContext, baggage: null));

        proxy.Input.Should().Be(input);
    }

    [Theory]
    [InlineData("bad\"value")]
    [InlineData("back\\slash")]
    [InlineData("\"closing-brace\"}")]
    [InlineData("\"},\"injected\":\"true")]
    [InlineData("tab\there")]
    [InlineData("nel\u0085char")]
    public async Task InjectContextIntoInput_MaliciousOrigin_ProducesParseableJsonAndRoundTrips(string maliciousOrigin)
    {
        var spanContext = CreateSpanContext(origin: maliciousOrigin);
        var request = new StartExecutionRequest { Input = """{"foo":"bar"}""" };
        var proxy = request.DuckCast<StartExecutionIntegration.IStartExecutionRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContextIntoInput<object, StartExecutionIntegration.IStartExecutionRequest>(
            tracer, proxy, new PropagationContext(spanContext, baggage: null));

        // result must be parseable JSON (no syntax break)
        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(proxy.Input!)!;
        parsed.Should().ContainKey("foo");
        parsed["foo"].Should().Be("bar");

        // no injection at the top level beyond the original keys and _datadog
        parsed.Keys.Should().BeEquivalentTo("foo", DatadogKey);

        // origin round-trips
        var datadog = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(parsed[DatadogKey]))!;
        datadog["x-datadog-origin"].Should().Be(maliciousOrigin);
    }

    [Theory]
    [InlineData("bad\"v")]
    [InlineData("back\\slash")]
    [InlineData("v\"},\"injected\":\"true")]
    public async Task InjectContextIntoInput_MaliciousBaggage_ProducesParseableJsonAndDoesNotInjectAtTopLevel(string maliciousValue)
    {
        var spanContext = CreateSpanContext(origin: null);
        var baggage = new Baggage { ["safe-key"] = maliciousValue };
        var request = new StartExecutionRequest { Input = """{"foo":"bar"}""" };
        var proxy = request.DuckCast<StartExecutionIntegration.IStartExecutionRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContextIntoInput<object, StartExecutionIntegration.IStartExecutionRequest>(
            tracer, proxy, new PropagationContext(spanContext, baggage));

        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(proxy.Input!)!;
        parsed.Keys.Should().BeEquivalentTo("foo", DatadogKey);
        parsed["foo"].Should().Be("bar");
    }

    private static SpanContext CreateSpanContext(string? origin)
    {
        var traceId = new TraceId(Upper: 1234567890123456789UL, Lower: 9876543210987654321UL);
        return new SpanContext(traceId, spanId: 6766950223540265769UL, samplingPriority: 1, serviceName: "test", origin: origin!);
    }
}
