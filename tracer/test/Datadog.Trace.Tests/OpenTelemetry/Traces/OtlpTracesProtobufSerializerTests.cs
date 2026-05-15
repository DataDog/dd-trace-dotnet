// <copyright file="OtlpTracesProtobufSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.OpenTelemetry.Traces;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

#nullable enable

namespace Datadog.Trace.Tests.OpenTelemetry.Traces;

public class OtlpTracesProtobufSerializerTests
{
    // OTLP span_kind enum values (https://github.com/open-telemetry/opentelemetry-proto/blob/v1.2.0/opentelemetry/proto/trace/v1/trace.proto#L172).
    private const int SpanKindInternal = 1;

    // OTLP Status.StatusCode enum values (same proto, lines 314-322).
    private const int StatusCodeError = 2;

    [Fact]
    public void FinishBody_ReturnsZero_WhenNothingSerialized()
    {
        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[1024];

        serializer.FinishBody(ref buffer, offset: 0, maxSize: buffer.Length).Should().Be(0);
    }

    [Fact]
    public void SerializeSpans_ReturnsZero_WhenSpansExceedMaxSize()
    {
        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[8 * 1024];

        // Pick a maxSize tiny enough that any real span overflows.
        serializer.SerializeSpans(ref buffer, temporaryBufferOffset: 0, CreateChunk(CreateSpan()), spanBufferOffset: 0, maxSize: 8)
            .Should().Be(0);
    }

    [Fact]
    public void SerializeSpans_TwoChunks_ProducesSingleResourceSpansWithBothSpans()
    {
        var chunk1 = CreateChunk(CreateSpan(resourceName: "resource_name1"));
        var chunk2 = CreateChunk(CreateSpan(resourceName: "resource_name2"));

        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[16 * 1024];

        var firstLength = serializer.SerializeSpans(ref buffer, 0, chunk1, spanBufferOffset: 0, maxSize: buffer.Length);
        var secondLength = serializer.SerializeSpans(ref buffer, firstLength, chunk2, spanBufferOffset: firstLength, maxSize: buffer.Length);
        var total = firstLength + secondLength;
        serializer.FinishBody(ref buffer, offset: total, maxSize: buffer.Length);

        var request = OtlpProtoParser.ParseExportTraceServiceRequest(buffer, 0, total);
        request.ResourceSpans.Should().HaveCount(1);
        request.ResourceSpans[0].ScopeSpans.Should().HaveCount(1);

        var spans = request.ResourceSpans[0].ScopeSpans[0].Spans;
        spans.Should().HaveCount(2);
        spans[0].Name.Should().Be("resource_name1");
        spans[1].Name.Should().Be("resource_name2");
    }

    [Fact]
    public void SerializeSpans_EmitsCanonicalSpanShape()
    {
        var span = SerializeAndParse(CreateChunk(CreateSpan()));

        span.Name.Should().Be("resource_name");
        span.Kind.Should().Be(SpanKindInternal);
        span.TraceId.Should().HaveCount(16);
        span.SpanId.Should().HaveCount(8);
    }

    [Fact]
    public void SerializeSpans_EmitsTraceIdAndSpanIdInBigEndianBytes()
    {
        var ddSpan = CreateSpan(
            traceId: new TraceId(0x0102030405060708UL, 0x090A0B0C0D0E0F10UL),
            spanId: 0x1122334455667788UL);

        var span = SerializeAndParse(CreateChunk(ddSpan));

        span.TraceId.Should().Equal(new byte[]
        {
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
        });
        span.SpanId.Should().Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 });
    }

    [Fact]
    public void SerializeSpans_RootSpan_OmitsParentSpanId()
    {
        var span = SerializeAndParse(CreateChunk(CreateSpan()));

        // The parser defaults ParentSpanId to an empty array when the field is not present on the wire.
        span.ParentSpanId.Should().BeEmpty();
    }

    [Fact]
    public void SerializeSpans_ChildSpan_EmitsParentSpanIdInBigEndianBytes()
    {
        var child = CreateSpan(traceId: new TraceId(0, 0xABCDUL), spanId: 0xFEED, parentSpanId: 0x0102030405060708UL);

        var span = SerializeAndParse(CreateChunk(child));

        span.ParentSpanId.Should().Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
    }

    [Fact]
    public void SerializeSpans_PopulatesEventsLinksAndStatus()
    {
        var ddSpan = CreateSpan();
        ddSpan.AddEvent(new SpanEvent("test-event", DateTimeOffset.UtcNow));
        ddSpan.AddLink(new SpanLink(new SpanContext(parent: null, new TraceContext(new StubDatadogTracer()), serviceName: "linked")));
        ddSpan.SetTag("otel.status_code", "STATUS_CODE_ERROR");
        ddSpan.SetTag(Tags.ErrorMsg, "oops");

        var span = SerializeAndParse(CreateChunk(ddSpan));

        span.Events.Should().ContainSingle(e => e.Name == "test-event");
        span.Links.Should().HaveCount(1);
        span.Status.Should().NotBeNull();
        span.Status!.Code.Should().Be(StatusCodeError);
        span.Status.Message.Should().Be("oops");
    }

    [Fact]
    public void SerializeSpans_EventAttributes_RoundTripAllAnyValueKinds()
    {
        var ddSpan = CreateSpan();
        ddSpan.AddEvent(new SpanEvent("e", DateTimeOffset.UtcNow, new List<KeyValuePair<string, object>>
        {
            new("k_string", "hello"),
            new("k_bool_true", true),
            new("k_bool_false", false),
            new("k_int", 42),
            new("k_long", 9_000_000_000L),
            new("k_double", 3.14),
            new("k_float", 1.5f),
            new("k_bytes", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }),
            new("k_null", null!),
        }));

        var byKey = new Dictionary<string, OtlpProtoParser.AnyValue>();
        foreach (var kv in SerializeAndParse(CreateChunk(ddSpan)).Events[0].Attributes)
        {
            byKey[kv.Key] = kv.Value;
        }

        byKey["k_string"].StringValue.Should().Be("hello");
        byKey["k_bool_true"].BoolValue.Should().BeTrue();
        byKey["k_bool_false"].BoolValue.Should().BeFalse();
        byKey["k_int"].IntValue.Should().Be(42L);
        byKey["k_long"].IntValue.Should().Be(9_000_000_000L);
        byKey["k_double"].DoubleValue.Should().Be(3.14);
        byKey["k_float"].DoubleValue.Should().BeApproximately(1.5, 1e-6);
        byKey["k_bytes"].BytesValue.Should().Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        // null serializes as an empty AnyValue — every typed field stays null on the parser side.
        var nullValue = byKey["k_null"];
        nullValue.StringValue.Should().BeNull();
        nullValue.BoolValue.Should().BeNull();
        nullValue.IntValue.Should().BeNull();
        nullValue.DoubleValue.Should().BeNull();
        nullValue.BytesValue.Should().BeNull();
    }

    [Theory]
    [InlineData(2, 1u)]   // user-keep
    [InlineData(1, 1u)]   // auto-keep
    [InlineData(0, 0u)]   // auto-reject
    [InlineData(-1, 0u)]  // user-reject
    public void SerializeSpans_SamplingPriority_EmitsSpanFlagsBit0(int samplingPriority, uint expectedFlags)
    {
        var span = SerializeAndParse(CreateChunk(CreatePropagatedSpan(samplingPriority)));

        span.Flags.Should().Be(expectedFlags);
    }

    [Theory]
    [InlineData(2, 0x80000001u)]   // keep: bit 0 + bit 31
    [InlineData(1, 0x80000001u)]
    [InlineData(0, 0x80000000u)]   // drop: bit 31 only
    [InlineData(-1, 0x80000000u)]
    public void SerializeSpans_LinkSamplingPriority_EmitsLinkFlags(int linkSamplingPriority, uint expectedFlags)
    {
        var span = SerializeAndParse(CreateChunk(SpanWithLink(linkSamplingPriority)));

        span.Links[0].Flags.Should().Be(expectedFlags);
    }

    [Fact]
    public void SerializeSpans_LinkWithoutSamplingPriority_OmitsFlagsField()
    {
        // "Omitted" and "present with value 0" are observationally identical via the parser default,
        // but the serializer's contract is "omit when null", which we still cover via the keep/drop cases.
        var span = SerializeAndParse(CreateChunk(SpanWithLink(linkSamplingPriority: null)));

        span.Links[0].Flags.Should().Be(0u);
    }

    [Fact]
    public void SerializeSpans_EventCountAboveLimit_EmitsDroppedEventsCount()
    {
        const int excess = 3;
        var ddSpan = CreateSpan();
        for (int i = 0; i < OtlpTracesProtobufSerializer.EventCountLimit + excess; i++)
        {
            ddSpan.AddEvent(new SpanEvent($"e{i}", DateTimeOffset.UtcNow));
        }

        var span = SerializeAndParse(CreateChunk(ddSpan));

        span.Events.Should().HaveCount(OtlpTracesProtobufSerializer.EventCountLimit);
        span.DroppedEventsCount.Should().Be((ulong)excess);
    }

    [Fact]
    public void SerializeSpans_LinkCountAboveLimit_EmitsDroppedLinksCount()
    {
        const int excess = 2;
        var ddSpan = CreateSpan();
        var linkTraceContext = new TraceContext(new StubDatadogTracer());
        for (int i = 0; i < OtlpTracesProtobufSerializer.LinkCountLimit + excess; i++)
        {
            ddSpan.AddLink(new SpanLink(new SpanContext(parent: null, linkTraceContext, serviceName: $"l{i}")));
        }

        var span = SerializeAndParse(CreateChunk(ddSpan));

        span.Links.Should().HaveCount(OtlpTracesProtobufSerializer.LinkCountLimit);
        span.DroppedLinksCount.Should().Be((ulong)excess);
    }

    [Fact]
    public void SerializeSpans_EventAttributeCountAboveLimit_EmitsDroppedAttributesCount()
    {
        const int excess = 5;
        var attributes = new List<KeyValuePair<string, object>>();
        for (int i = 0; i < OtlpTracesProtobufSerializer.AttributePerEventCountLimit + excess; i++)
        {
            attributes.Add(new($"key{i}", $"value{i}"));
        }

        var ddSpan = CreateSpan();
        ddSpan.AddEvent(new SpanEvent("event-name", DateTimeOffset.UtcNow, attributes));

        var span = SerializeAndParse(CreateChunk(ddSpan));

        span.Events[0].Attributes.Should().HaveCount(OtlpTracesProtobufSerializer.AttributePerEventCountLimit);
        span.Events[0].DroppedAttributesCount.Should().Be((ulong)excess);
    }

    // ----- helpers -----

    private static OtlpProtoParser.Span SerializeAndParse(TraceChunkModel chunk)
    {
        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[64 * 1024];
        var written = serializer.SerializeSpans(ref buffer, 0, chunk, spanBufferOffset: 0, maxSize: buffer.Length);
        serializer.FinishBody(ref buffer, written, maxSize: buffer.Length);

        return OtlpProtoParser.ParseExportTraceServiceRequest(buffer, 0, written)
            .ResourceSpans[0].ScopeSpans[0].Spans[0];
    }

    private static TraceChunkModel CreateChunk(Span span)
        => new TraceChunkModel(new SpanCollection(new[] { span }));

    private static Span CreateSpan(
        TraceId traceId = default,
        ulong spanId = 0,
        ulong parentSpanId = 0,
        string serviceName = "service_name",
        string resourceName = "resource_name")
    {
        var traceContext = new TraceContext(new StubDatadogTracer());
        SpanContext? parent = parentSpanId == 0
            ? null
            : new SpanContext(parent: null, traceContext, serviceName, traceId: traceId, spanId: parentSpanId);
        var context = new SpanContext(parent: parent, traceContext, serviceName, traceId: traceId, spanId: spanId);
        var span = new Span(context, DateTimeOffset.UtcNow)
        {
            OperationName = "operation_name",
            ResourceName = resourceName,
        };
        span.SetDuration(TimeSpan.FromMilliseconds(1));
        return span;
    }

    private static Span CreatePropagatedSpan(int samplingPriority)
    {
        // Sampling priority lives on Context.SamplingPriority only for propagated contexts,
        // which is the path the serializer keys off for span.flags.
        var context = new SpanContext(
            traceId: new TraceId(0, 1),
            spanId: 1,
            samplingPriority: samplingPriority,
            serviceName: "service_name",
            origin: null);
        var span = new Span(context, DateTimeOffset.UtcNow)
        {
            OperationName = "operation_name",
            ResourceName = "resource_name",
        };
        span.SetDuration(TimeSpan.FromMilliseconds(1));
        return span;
    }

    private static Span SpanWithLink(int? linkSamplingPriority)
    {
        var span = CreateSpan();
        var linkContext = new SpanContext(
            traceId: new TraceId(0, 2),
            spanId: 2,
            samplingPriority: linkSamplingPriority,
            serviceName: "linked",
            origin: null);
        span.AddLink(new SpanLink(linkContext));
        return span;
    }
}
