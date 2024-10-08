// <copyright file="ContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Text;
using Amazon.EventBridge.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.EventBridge;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.EventBridge;

public class ContextPropagationTests
{
    private const string DatadogKey = "_datadog";
    private const string StartTimeKey = "x-datadog-start-time";
    private const string ResourceNameKey = "x-datadog-resource-name";
    private const string EventBusName = "test-event-bus";
    private const int MaxSizeBytes = 256 * 1024; // 256 KB

    private readonly SpanContext _spanContext;

    public ContextPropagationTests()
    {
        const long upper = 1234567890123456789;
        const ulong lower = 9876543210987654321;

        var traceId = new TraceId(upper, lower);
        const ulong spanId = 6766950223540265769;
        _spanContext = new SpanContext(traceId, spanId, 1, "test-eventbridge", "serverless");
    }

    [Fact]
    public void InjectTracingContext_EmptyDetail_AddsTraceContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{}", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        ContextPropagation.InjectTracingContext(proxy, _spanContext);

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
    public void InjectTracingContext_ExistingDetail_AddsTraceContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{\"foo\":\"bar\"}", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        ContextPropagation.InjectTracingContext(proxy, _spanContext);

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
    public void InjectTracingContext_NullDetail_AddsTraceContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = null, EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        ContextPropagation.InjectTracingContext(proxy, _spanContext);

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
    public void InjectTracingContext_InvalidDetail_DoesNotAddTraceContext()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{invalid json", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        ContextPropagation.InjectTracingContext(proxy, _spanContext);

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        entry.Detail.Should().Be("{invalid json");
    }

    [Fact]
    public void InjectTracingContext_MultipleEntries_AddsTraceContextToAll()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{}", EventBusName = EventBusName },
            new PutEventsRequestEntry { Detail = "{\"foo\":\"bar\"}", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        ContextPropagation.InjectTracingContext(proxy, _spanContext);

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
    public void InjectTracingContext_NullEventBusName_OmitsResourceName()
    {
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = "{}", EventBusName = null }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        ContextPropagation.InjectTracingContext(proxy, _spanContext);

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
    public void InjectTracingContext_PayloadTooLarge_DoesNotAddTraceContext()
    {
        var largeDetail = new string('a', MaxSizeBytes);
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = $"{{{largeDetail}}}", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        ContextPropagation.InjectTracingContext(proxy, _spanContext);

        var entries = (IList)proxy.Entries.Value!;
        entries.Count.Should().Be(1);
        var entry = (PutEventsRequestEntry)entries[0]!;

        entry.Detail.Should().Be($"{{{largeDetail}}}");
    }

    [Fact]
    public void InjectTracingContext_PayloadJustUnderLimit_AddsTraceContext()
    {
        var detailSize = MaxSizeBytes - 1000; // Leave some room for the trace context
        var largeDetail = new string('a', detailSize);
        var request = GeneratePutEventsRequest([
            new PutEventsRequestEntry { Detail = $"{{\"large\":\"{largeDetail}\"}}", EventBusName = EventBusName }
        ]);

        var proxy = request.DuckCast<IPutEventsRequest>();

        ContextPropagation.InjectTracingContext(proxy, _spanContext);

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

    private static PutEventsRequest GeneratePutEventsRequest(List<PutEventsRequestEntry> entries)
    {
        return new PutEventsRequest { Entries = entries };
    }
}
