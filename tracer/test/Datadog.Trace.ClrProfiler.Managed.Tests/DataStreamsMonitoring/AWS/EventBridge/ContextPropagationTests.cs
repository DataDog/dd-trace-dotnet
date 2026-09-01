// <copyright file="ContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.EventBridge.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.EventBridge;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.EventBridge;

public class ContextPropagationTests
{
    private const string DatadogKey = "_datadog";
    private const string DataStreamsContextKey = "dd-pathway-ctx-base64";
    private const string DetailType = "test-detail-type";
    private const string ServiceName = "test-eventbridge";
    private const string StartTimeKey = "x-datadog-start-time";
    private const string ResourceNameKey = "x-datadog-resource-name";
    private const string EventBusName = "test-event-bus";
    private const string EventBusPathwayHash = "4499620863636772640";
    private const string DefaultBusPathwayHash = "12415672181339992293";
    private const int EventBusInjectedPayloadSizeBytes = 431;
    private const int DefaultBusInjectedPayloadSizeBytes = 388;
    private const int MaxSizeBytes = 256 * 1024; // 256 KB
    private const long TraceIdUpper = 1234567890123456789;
    private const ulong TraceIdLower = 9876543210987654321;
    private const ulong SpanId = 6766950223540265769;

    private readonly SpanContext _spanContext;

    public ContextPropagationTests()
    {
        _spanContext = CreateFixedSpanContext(origin: "serverless");
        ResetLastConsumePathway();
    }

    [Fact]
    public async Task InjectTracingContext_EmptyDetail_AddsTraceContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{}", DetailType = DetailType, EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContext(tracer, proxy, scope: null, new PropagationContext(_spanContext, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        var detail = JsonConvert.DeserializeObject<Dictionary<string, object>>(entry.Detail);
        detail.Should().NotBeNull();
        detail!.Count.Should().Be(1);

        var extracted = detail.TryGetValue(DatadogKey, out var datadogObject);
        extracted.Should().BeTrue();
        datadogObject.Should().NotBeNull();

        var jsonString = JsonConvert.SerializeObject(datadogObject);
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

        extractedTraceContext!["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
        extractedTraceContext[ResourceNameKey].Should().Be(EventBusName);
        extractedTraceContext.Should().ContainKey(StartTimeKey);
        extractedTraceContext[StartTimeKey].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InjectTracingContext_ExistingDetail_AddsTraceContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = """{"foo":"bar"}""", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContext(tracer, proxy, scope: null, new PropagationContext(_spanContext, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        var detail = JsonConvert.DeserializeObject<Dictionary<string, object>>(entry.Detail);
        detail.Should().NotBeNull();
        detail!.Count.Should().Be(2);
        detail["foo"].Should().Be("bar");

        var extracted = detail.TryGetValue(DatadogKey, out var datadogObject);
        extracted.Should().BeTrue();
        datadogObject.Should().NotBeNull();

        var jsonString = JsonConvert.SerializeObject(datadogObject);
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

        extractedTraceContext!["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
        extractedTraceContext[ResourceNameKey].Should().Be(EventBusName);
        extractedTraceContext.Should().ContainKey(StartTimeKey);
        extractedTraceContext[StartTimeKey].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InjectTracingContext_NullDetail_AddsTraceContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = null, EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContext(tracer, proxy, scope: null, new PropagationContext(_spanContext, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        var detail = JsonConvert.DeserializeObject<Dictionary<string, object>>(entry.Detail);
        detail.Should().NotBeNull();
        detail!.Count.Should().Be(1);

        var extracted = detail.TryGetValue(DatadogKey, out var datadogObject);
        extracted.Should().BeTrue();
        datadogObject.Should().NotBeNull();

        var jsonString = JsonConvert.SerializeObject(datadogObject);
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

        extractedTraceContext!["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
        extractedTraceContext[ResourceNameKey].Should().Be(EventBusName);
        extractedTraceContext.Should().ContainKey(StartTimeKey);
        extractedTraceContext[StartTimeKey].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InjectTracingContext_InvalidDetail_DoesNotAddTraceContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{invalid json", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContext(tracer, proxy, scope: null, new PropagationContext(_spanContext, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        entry.Detail.Should().Be("{invalid json");
    }

    [Fact]
    public async Task InjectTracingContext_MultipleEntries_AddsTraceContextToAll()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{}", EventBusName = EventBusName },
            new PutEventsRequestEntry { Detail = """{"foo":"bar"}""", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContext(tracer, proxy, scope: null, new PropagationContext(_spanContext, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(2);

        foreach (var entry in entries)
        {
            var typedEntry = entry as PutEventsRequestEntry;
            var detail = JsonConvert.DeserializeObject<Dictionary<string, object>>(typedEntry!.Detail);
            detail.Should().NotBeNull();
            detail!.Should().ContainKey(DatadogKey);

            var extracted = detail!.TryGetValue(DatadogKey, out var datadogObject);
            extracted.Should().BeTrue();
            datadogObject.Should().NotBeNull();

            var jsonString = JsonConvert.SerializeObject(datadogObject);
            var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

            extractedTraceContext!["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
            extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
            extractedTraceContext[ResourceNameKey].Should().Be(EventBusName);
            extractedTraceContext.Should().ContainKey(StartTimeKey);
            extractedTraceContext[StartTimeKey].Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task InjectTracingContext_NullEventBusName_OmitsResourceName()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{}", EventBusName = null }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContext(tracer, proxy, scope: null, new PropagationContext(_spanContext, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        var detail = JsonConvert.DeserializeObject<Dictionary<string, object>>(entry.Detail);
        detail.Should().NotBeNull();
        detail!.Count.Should().Be(1);

        var extracted = detail.TryGetValue(DatadogKey, out var datadogObject);
        extracted.Should().BeTrue();
        datadogObject.Should().NotBeNull();

        var jsonString = JsonConvert.SerializeObject(datadogObject);
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

        extractedTraceContext!["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
        extractedTraceContext.Should().NotContainKey(ResourceNameKey);
        extractedTraceContext.Should().ContainKey(StartTimeKey);
        extractedTraceContext[StartTimeKey].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InjectTracingContext_PayloadTooLarge_DoesNotAddTraceContext()
    {
        var largeDetail = new string('a', MaxSizeBytes);
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = $"{{{largeDetail}}}", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContext(tracer, proxy, scope: null, new PropagationContext(_spanContext, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        entry.Detail.Should().Be($"{{{largeDetail}}}");
    }

    [Fact]
    public async Task InjectTracingContext_PayloadJustUnderLimit_AddsTraceContext()
    {
        var detailSize = MaxSizeBytes - 1000; // Leave some room for the trace context
        var largeDetail = new string('a', detailSize);
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = $"{{\"large\":\"{largeDetail}\"}}", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContext(tracer, proxy, scope: null, new PropagationContext(_spanContext, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        var detail = JsonConvert.DeserializeObject<Dictionary<string, object>>(entry.Detail);
        detail.Should().NotBeNull();
        detail!.Count.Should().Be(2);
        detail.Should().ContainKey("large");
        detail.Should().ContainKey(DatadogKey);

        var byteSize = Encoding.UTF8.GetByteCount(entry.Detail);
        byteSize.Should().BeLessThan(MaxSizeBytes);
    }

    [Theory]
    [InlineData("bad\"value")]
    [InlineData("back\\slash")]
    [InlineData("\"closing-brace\"}")]
    [InlineData("\"},\"injected\":\"true")]
    [InlineData("tab\there")]
    [InlineData("nel\u0085char")]
    public async Task InjectTracingContext_MaliciousOrigin_ProducesParseableJsonAndRoundTrips(string maliciousOrigin)
    {
        var spanContext = new SpanContext(
            new TraceId(Upper: 1234567890123456789UL, Lower: 9876543210987654321UL),
            spanId: 6766950223540265769UL,
            samplingPriority: 1,
            serviceName: "test-eventbridge",
            origin: maliciousOrigin);

        var entry = new PutEventsRequestEntry { Detail = """{"foo":"bar"}""", EventBusName = EventBusName };
        var request = GeneratePutEventsRequest([entry]);
        var proxy = request.DuckCast<IPutEventsRequest>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectContext(tracer, proxy, scope: null, new PropagationContext(spanContext, baggage: null));

        var detail = JsonConvert.DeserializeObject<Dictionary<string, object>>(entry.Detail)!;
        detail.Keys.Should().BeEquivalentTo("foo", DatadogKey);
        detail["foo"].Should().Be("bar");

        var datadog = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(detail[DatadogKey]))!;
        datadog["x-datadog-origin"].Should().Be(maliciousOrigin);
    }

    [Fact]
    public async Task InjectTracingContext_WithDsmEnabled_AddsPathwayContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{}", DetailType = DetailType, EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();
        var settings = CreateDsmSettings();

        await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
        using var scope = CreateDsmScope();

        ContextPropagation.InjectContext(tracer, proxy, scope, new PropagationContext(scope.Span.Context, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        var detail = JsonConvert.DeserializeObject<Dictionary<string, object>>(entry.Detail);
        detail.Should().NotBeNull();
        var detailDictionary = detail!;
        detailDictionary.Should().ContainKey(DatadogKey);

        var extracted = detailDictionary.TryGetValue(DatadogKey, out var datadogObject);
        extracted.Should().BeTrue();
        datadogObject.Should().NotBeNull();

        var jsonString = JsonConvert.SerializeObject(datadogObject);
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
        var extractedTraceContextDictionary = extractedTraceContext!;

        extractedTraceContextDictionary.Should().ContainKey(DataStreamsContextKey);
        extractedTraceContextDictionary[DataStreamsContextKey].Should().NotBeNull();
        var encodedPathway = extractedTraceContextDictionary[DataStreamsContextKey].ToString();
        encodedPathway.Should().NotBeNullOrEmpty();
        System.Convert.FromBase64String(encodedPathway!).Should().NotBeEmpty();
        scope.Span.GetTag("pathway.hash").Should().Be(EventBusPathwayHash);
        Encoding.UTF8.GetByteCount(entry.Detail).Should().Be(EventBusInjectedPayloadSizeBytes);
    }

    [Fact]
    public async Task InjectTracingContext_WithDsmEnabled_AndDefaultBus_AddsPathwayContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{}", DetailType = DetailType, EventBusName = null }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();
        var settings = CreateDsmSettings();

        await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
        using var scope = CreateDsmScope();

        ContextPropagation.InjectContext(tracer, proxy, scope, new PropagationContext(scope.Span.Context, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        var detail = JsonConvert.DeserializeObject<Dictionary<string, object>>(entry.Detail);
        detail.Should().NotBeNull();
        var detailDictionary = detail!;
        detailDictionary.Should().ContainKey(DatadogKey);

        var extracted = detailDictionary.TryGetValue(DatadogKey, out var datadogObject);
        extracted.Should().BeTrue();
        datadogObject.Should().NotBeNull();

        var jsonString = JsonConvert.SerializeObject(datadogObject);
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
        var extractedTraceContextDictionary = extractedTraceContext!;

        extractedTraceContextDictionary.Should().ContainKey(DataStreamsContextKey);
        extractedTraceContextDictionary.Should().NotContainKey(ResourceNameKey);
        extractedTraceContextDictionary[DataStreamsContextKey].Should().NotBeNull();
        scope.Span.GetTag("pathway.hash").Should().Be(DefaultBusPathwayHash);
        Encoding.UTF8.GetByteCount(entry.Detail).Should().Be(DefaultBusInjectedPayloadSizeBytes);
    }

    [Fact]
    public async Task InjectTracingContext_WithDsmEnabled_AndInvalidDetail_DoesNotCreatePathwayContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{invalid json", DetailType = DetailType, EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();
        var settings = CreateDsmSettings();

        await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
        using var scope = CreateDsmScope();

        ContextPropagation.InjectContext(tracer, proxy, scope, new PropagationContext(scope.Span.Context, baggage: null));

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        entry.Detail.Should().Be("{invalid json");
        scope.Span.GetTag("pathway.hash").Should().BeNull();
    }

    private static PutEventsRequest GeneratePutEventsRequest(List<PutEventsRequestEntry> entries)
    {
        return new PutEventsRequest { Entries = entries };
    }

    private static TracerSettings CreateDsmSettings()
    {
        return TracerSettings.Create(new()
        {
            { ConfigurationKeys.DataStreamsMonitoring.Enabled, true },
            { ConfigurationKeys.PropagateProcessTags, false },
            { ConfigurationKeys.ServiceName, ServiceName },
        });
    }

    private static Scope CreateDsmScope()
    {
        var span = new Span(CreateFixedSpanContext(), DateTimeOffset.UtcNow);
        return new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: false);
    }

    private static SpanContext CreateFixedSpanContext(string origin = "")
    {
        return new SpanContext(new TraceId(TraceIdUpper, TraceIdLower), SpanId, 1, ServiceName, origin);
    }

    private static void ResetLastConsumePathway()
    {
        var field = typeof(DataStreamsMonitoring.DataStreamsManager).GetField("LastConsumePathway", BindingFlags.NonPublic | BindingFlags.Static);
        var lastConsumePathway = (AsyncLocal<DataStreamsMonitoring.PathwayContext?>)field!.GetValue(null)!;
        lastConsumePathway.Value = null;
    }
}
