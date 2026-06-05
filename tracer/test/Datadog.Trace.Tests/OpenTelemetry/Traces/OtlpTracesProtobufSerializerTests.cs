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
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using Xunit;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;
using OtlpStatusCode = OpenTelemetry.Proto.Trace.V1.Status.Types.StatusCode;

#nullable enable

namespace Datadog.Trace.Tests.OpenTelemetry.Traces;

public class OtlpTracesProtobufSerializerTests
{
    [Fact]
    public void FinishBody_ReturnsZero_WhenNothingSerialized()
    {
        var serializer = new OtlpTracesProtobufSerializer(openTelemetryTraceCompatibilityEnabled: false);
        var buffer = new byte[1024];

        serializer.FinishBody(ref buffer, offset: 0, maxSize: buffer.Length).Should().Be(0);
    }

    [Fact]
    public void SerializeSpans_ReturnsZero_WhenSpansExceedMaxSize()
    {
        var serializer = new OtlpTracesProtobufSerializer(openTelemetryTraceCompatibilityEnabled: false);
        var buffer = new byte[8 * 1024];

        // Pick a maxSize tiny enough that any real span overflows.
        // The buffer is large enough that no write throws; overflow is detected by the
        // post-write byte count check.
        serializer.SerializeSpans(ref buffer, temporaryBufferOffset: 0, CreateChunk(CreateSpan()), spanBufferOffset: 0, maxSize: 8)
            .Should().Be(0);
    }

    [Fact]
    public void SerializeSpans_ReturnsZero_WhenWritesExceedBufferCapacity()
    {
        var serializer = new OtlpTracesProtobufSerializer(openTelemetryTraceCompatibilityEnabled: false);

        // Buffer is smaller than a single span's serialized form, so per-field writes
        // will throw IndexOutOfRangeException mid-serialization. The catch must roll
        // back and return 0 rather than letting the exception escape.
        var buffer = new byte[8];

        serializer.SerializeSpans(ref buffer, temporaryBufferOffset: 0, CreateChunk(CreateSpan()), spanBufferOffset: 0, maxSize: 8)
            .Should().Be(0);

        // After a failed serialize, retrying into a fresh buffer should succeed —
        // proves the length-position fields were rolled back.
        var fresh = new byte[16 * 1024];
        var written = serializer.SerializeSpans(ref fresh, temporaryBufferOffset: 0, CreateChunk(CreateSpan()), spanBufferOffset: 0, maxSize: fresh.Length);
        written.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SerializeSpans_TwoChunks_ProducesSingleResourceSpansWithBothSpans()
    {
        var chunk1 = CreateChunk(CreateSpan(resourceName: "resource_name1"));
        var chunk2 = CreateChunk(CreateSpan(resourceName: "resource_name2"));

        var serializer = new OtlpTracesProtobufSerializer(openTelemetryTraceCompatibilityEnabled: false);
        var buffer = new byte[16 * 1024];

        var firstLength = serializer.SerializeSpans(ref buffer, 0, chunk1, spanBufferOffset: 0, maxSize: buffer.Length);
        var secondLength = serializer.SerializeSpans(ref buffer, firstLength, chunk2, spanBufferOffset: firstLength, maxSize: buffer.Length);
        var total = firstLength + secondLength;
        serializer.FinishBody(ref buffer, offset: total, maxSize: buffer.Length);

        var request = ParseRequest(buffer, total);
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
        span.Kind.Should().Be(OtlpSpan.Types.SpanKind.Internal);
        span.TraceId.Length.Should().Be(16);
        span.SpanId.Length.Should().Be(8);
    }

    [Fact]
    public void SerializeSpans_EmitsTraceIdAndSpanIdInBigEndianBytes()
    {
        var ddSpan = CreateSpan(
            traceId: new TraceId(0x0102030405060708UL, 0x090A0B0C0D0E0F10UL),
            spanId: 0x1122334455667788UL);

        var span = SerializeAndParse(CreateChunk(ddSpan));

        span.TraceId.ToByteArray().Should().Equal(new byte[]
        {
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
        });
        span.SpanId.ToByteArray().Should().Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 });
    }

    [Fact]
    public void SerializeSpans_RootSpan_OmitsParentSpanId()
    {
        var span = SerializeAndParse(CreateChunk(CreateSpan()));

        // ByteString defaults to Empty when the field is not present on the wire.
        span.ParentSpanId.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void SerializeSpans_ChildSpan_EmitsParentSpanIdInBigEndianBytes()
    {
        var child = CreateSpan(traceId: new TraceId(0, 0xABCDUL), spanId: 0xFEED, parentSpanId: 0x0102030405060708UL);

        var span = SerializeAndParse(CreateChunk(child));

        span.ParentSpanId.ToByteArray().Should().Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
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
        span.Status.Code.Should().Be(OtlpStatusCode.Error);
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

        var byKey = new Dictionary<string, AnyValue>();
        foreach (var kv in SerializeAndParse(CreateChunk(ddSpan)).Events[0].Attributes)
        {
            byKey[kv.Key] = kv.Value;
        }

        byKey["k_string"].ValueCase.Should().Be(AnyValue.ValueOneofCase.StringValue);
        byKey["k_string"].StringValue.Should().Be("hello");

        byKey["k_bool_true"].ValueCase.Should().Be(AnyValue.ValueOneofCase.BoolValue);
        byKey["k_bool_true"].BoolValue.Should().BeTrue();

        byKey["k_bool_false"].ValueCase.Should().Be(AnyValue.ValueOneofCase.BoolValue);
        byKey["k_bool_false"].BoolValue.Should().BeFalse();

        byKey["k_int"].ValueCase.Should().Be(AnyValue.ValueOneofCase.IntValue);
        byKey["k_int"].IntValue.Should().Be(42L);

        byKey["k_long"].ValueCase.Should().Be(AnyValue.ValueOneofCase.IntValue);
        byKey["k_long"].IntValue.Should().Be(9_000_000_000L);

        byKey["k_double"].ValueCase.Should().Be(AnyValue.ValueOneofCase.DoubleValue);
        byKey["k_double"].DoubleValue.Should().Be(3.14);

        byKey["k_float"].ValueCase.Should().Be(AnyValue.ValueOneofCase.DoubleValue);
        byKey["k_float"].DoubleValue.Should().BeApproximately(1.5, 1e-6);

        byKey["k_bytes"].ValueCase.Should().Be(AnyValue.ValueOneofCase.BytesValue);
        byKey["k_bytes"].BytesValue.ToByteArray().Should().Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        // null serializes as an empty AnyValue — no oneof field is set.
        byKey["k_null"].ValueCase.Should().Be(AnyValue.ValueOneofCase.None);
    }

    [Theory]
    [InlineData((byte)5, 5L)]
    [InlineData((sbyte)-5, -5L)]
    [InlineData((short)-30000, -30000L)]
    [InlineData((ushort)60000, 60000L)]
    [InlineData(int.MinValue, (long)int.MinValue)]
    [InlineData(uint.MaxValue, (long)uint.MaxValue)]
    [InlineData(long.MaxValue, long.MaxValue)]
    public void EventAttribute_IntegralType_EmitsAsIntValue(object input, long expected)
    {
        var attr = SerializeEventAttribute("k", input);

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.IntValue);
        attr.IntValue.Should().Be(expected);
    }

    [Theory]
    [InlineData('a', "a")]
    [InlineData('Z', "Z")]
    public void EventAttribute_Char_EmitsAsStringValue(char input, string expected)
    {
        var attr = SerializeEventAttribute("k", input);

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.StringValue);
        attr.StringValue.Should().Be(expected);
    }

    [Fact]
    public void EventAttribute_Ulong_FallsBackToStringValue()
    {
        // ulong may overflow long, so OTel's TagWriter falls back to ToString.
        var attr = SerializeEventAttribute("k", ulong.MaxValue);

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.StringValue);
        attr.StringValue.Should().Be(ulong.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void EventAttribute_Decimal_FallsBackToStringValue()
    {
        // decimal → double would lose precision, so OTel's TagWriter falls back to ToString.
        var attr = SerializeEventAttribute("k", 1.25m);

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.StringValue);
        attr.StringValue.Should().Be("1.25");
    }

    [Fact]
    public void EventAttribute_StringArray_EmitsAsArrayOfStringValues()
    {
        var attr = SerializeEventAttribute("k", new[] { "a", "b", "c" });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(3);
        attr.ArrayValue.Values[0].StringValue.Should().Be("a");
        attr.ArrayValue.Values[1].StringValue.Should().Be("b");
        attr.ArrayValue.Values[2].StringValue.Should().Be("c");
    }

    [Fact]
    public void EventAttribute_StringArrayWithNullElement_EmitsNullAnyValueInPlace()
    {
        var attr = SerializeEventAttribute("k", new string?[] { "a", null, "c" });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(3);
        attr.ArrayValue.Values[0].StringValue.Should().Be("a");
        attr.ArrayValue.Values[1].ValueCase.Should().Be(AnyValue.ValueOneofCase.None);
        attr.ArrayValue.Values[2].StringValue.Should().Be("c");
    }

    [Fact]
    public void EventAttribute_BoolArray_EmitsAsArrayOfBoolValues()
    {
        var attr = SerializeEventAttribute("k", new[] { true, false, true });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(3);
        attr.ArrayValue.Values[0].BoolValue.Should().BeTrue();
        attr.ArrayValue.Values[1].BoolValue.Should().BeFalse();
        attr.ArrayValue.Values[2].BoolValue.Should().BeTrue();
    }

    [Fact]
    public void EventAttribute_IntArray_EmitsAsArrayOfIntValues()
    {
        var attr = SerializeEventAttribute("k", new[] { 1, 2, 3 });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(3);
        attr.ArrayValue.Values[0].IntValue.Should().Be(1L);
        attr.ArrayValue.Values[1].IntValue.Should().Be(2L);
        attr.ArrayValue.Values[2].IntValue.Should().Be(3L);
    }

    [Fact]
    public void EventAttribute_LongArray_EmitsAsArrayOfIntValues()
    {
        var attr = SerializeEventAttribute("k", new[] { 1L, 9_000_000_000L, -42L });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(3);
        attr.ArrayValue.Values[0].IntValue.Should().Be(1L);
        attr.ArrayValue.Values[1].IntValue.Should().Be(9_000_000_000L);
        attr.ArrayValue.Values[2].IntValue.Should().Be(-42L);
    }

    [Fact]
    public void EventAttribute_DoubleArray_EmitsAsArrayOfDoubleValues()
    {
        var attr = SerializeEventAttribute("k", new[] { 1.5, 2.5, 3.5 });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(3);
        attr.ArrayValue.Values[0].DoubleValue.Should().Be(1.5);
        attr.ArrayValue.Values[1].DoubleValue.Should().Be(2.5);
        attr.ArrayValue.Values[2].DoubleValue.Should().Be(3.5);
    }

    [Fact]
    public void EventAttribute_FloatArray_EmitsAsArrayOfDoubleValues()
    {
        var attr = SerializeEventAttribute("k", new[] { 1.5f, 2.5f });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(2);
        attr.ArrayValue.Values[0].DoubleValue.Should().BeApproximately(1.5, 1e-6);
        attr.ArrayValue.Values[1].DoubleValue.Should().BeApproximately(2.5, 1e-6);
    }

    [Fact]
    public void EventAttribute_CharArray_EmitsAsArrayOfStringValues()
    {
        var attr = SerializeEventAttribute("k", new[] { 'a', 'b' });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(2);
        attr.ArrayValue.Values[0].StringValue.Should().Be("a");
        attr.ArrayValue.Values[1].StringValue.Should().Be("b");
    }

    [Fact]
    public void EventAttribute_EmptyArray_EmitsEmptyArrayValue()
    {
        var attr = SerializeEventAttribute("k", Array.Empty<int>());

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().BeEmpty();
    }

    [Fact]
    public void EventAttribute_ObjectArrayMixedTypes_EmitsMixedAnyValueElements()
    {
        var attr = SerializeEventAttribute("k", new object?[] { "a", true, 42, 3.14, null });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(5);
        attr.ArrayValue.Values[0].StringValue.Should().Be("a");
        attr.ArrayValue.Values[1].BoolValue.Should().BeTrue();
        attr.ArrayValue.Values[2].IntValue.Should().Be(42L);
        attr.ArrayValue.Values[3].DoubleValue.Should().Be(3.14);
        attr.ArrayValue.Values[4].ValueCase.Should().Be(AnyValue.ValueOneofCase.None);
    }

    [Fact]
    public void EventAttribute_ByteArray_StillEmitsAsBytesValue()
    {
        // byte[] must short-circuit array handling and emit as bytes, not an array of ints.
        var attr = SerializeEventAttribute("k", new byte[] { 0x01, 0x02, 0x03 });

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.BytesValue);
        attr.BytesValue.ToByteArray().Should().Equal(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Fact]
    public void EventAttribute_SelfReferentialObjectArray_IsBoundedAtOneLevel()
    {
        var cycle = new object[1];
        cycle[0] = cycle;

        var attr = SerializeEventAttribute("k", cycle);

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(1);
        attr.ArrayValue.Values[0].StringValue.Should().Be(cycle.ToString());
    }

    [Fact]
    public void EventAttribute_DeeplyNestedArray_IsBoundedAtOneLevel()
    {
        object[] current = new object[] { "leaf" };
        for (int i = 0; i < 5_000; i++)
        {
            current = new object[] { current };
        }

        var attr = SerializeEventAttribute("k", current);

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(1);
        attr.ArrayValue.Values[0].StringValue.Should().Be(typeof(object[]).ToString());
    }

    [Fact]
    public void EventAttribute_NestedObjectArray_StringifiesInsteadOfArrayValue()
    {
        var inner = new object[] { "a", 1 };
        var outer = new object[] { inner };

        var attr = SerializeEventAttribute("k", outer);

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(1);
        attr.ArrayValue.Values[0].StringValue.Should().Be(inner.ToString());
    }

    [Fact]
    public void EventAttribute_NestedByteArray_StringifiesInsteadOfBytesValue()
    {
        // Top-level byte[] still emits BytesValue (see EventAttribute_ByteArray_StillEmitsAsBytesValue).
        var nestedBytes = new byte[] { 0x01, 0x02 };
        var outer = new object[] { nestedBytes };

        var attr = SerializeEventAttribute("k", outer);

        attr.ValueCase.Should().Be(AnyValue.ValueOneofCase.ArrayValue);
        attr.ArrayValue.Values.Should().HaveCount(1);
        attr.ArrayValue.Values[0].StringValue.Should().Be(nestedBytes.ToString());
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
        span.DroppedEventsCount.Should().Be((uint)excess);
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
        span.DroppedLinksCount.Should().Be((uint)excess);
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
        span.Events[0].DroppedAttributesCount.Should().Be((uint)excess);
    }

    // ----- helpers -----

    private static AnyValue SerializeEventAttribute(string key, object? value)
    {
        var ddSpan = CreateSpan();
        ddSpan.AddEvent(new SpanEvent("e", DateTimeOffset.UtcNow, new List<KeyValuePair<string, object>>
        {
            new(key, value!),
        }));

        var attributes = SerializeAndParse(CreateChunk(ddSpan)).Events[0].Attributes;
        attributes.Should().ContainSingle(a => a.Key == key);
        return attributes[0].Value;
    }

    private static OtlpSpan SerializeAndParse(TraceChunkModel chunk)
    {
        var serializer = new OtlpTracesProtobufSerializer(openTelemetryTraceCompatibilityEnabled: false);
        var buffer = new byte[64 * 1024];
        var written = serializer.SerializeSpans(ref buffer, 0, chunk, spanBufferOffset: 0, maxSize: buffer.Length);
        serializer.FinishBody(ref buffer, written, maxSize: buffer.Length);

        return ParseRequest(buffer, written)
            .ResourceSpans[0].ScopeSpans[0].Spans[0];
    }

    private static ExportTraceServiceRequest ParseRequest(byte[] buffer, int length)
        => ExportTraceServiceRequest.Parser.ParseFrom(buffer, 0, length);

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
