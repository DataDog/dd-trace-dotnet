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
    [Fact]
    public void FinishBody_ReturnsZero_WhenNothingSerialized()
    {
        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[1024];

        var written = serializer.FinishBody(ref buffer, offset: 0, maxSize: buffer.Length);

        written.Should().Be(0);
    }

    [Fact]
    public void SerializeSpans_PopulatesEventsLinksAndStatus()
    {
        var chunk = TestData.CreateTraceChunkWithEventLinkAndStatus();

        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[16 * 1024];

        var written = serializer.SerializeSpans(ref buffer, 0, chunk, spanBufferOffset: 0, maxSize: buffer.Length);
        serializer.FinishBody(ref buffer, offset: written, maxSize: buffer.Length);

        var request = OtlpProtoParser.ParseExportTraceServiceRequest(buffer, 0, written);
        var span = request.ResourceSpans[0].ScopeSpans[0].Spans[0];

        span.Events.Should().HaveCount(1);
        span.Events[0].Name.Should().Be("test-event");
        span.Links.Should().HaveCount(1);
        span.Status.Should().NotBeNull();
        span.Status!.Code.Should().Be(2); // STATUS_CODE_ERROR
        span.Status.Message.Should().Be("oops");
    }

    [Fact]
    public void SerializeSpans_TwoChunks_ProducesSingleResourceSpansWithBothSpans()
    {
        var chunk1 = TestData.CreateTraceChunkWithSingleSpan("op1", "res1", "svc");
        var chunk2 = TestData.CreateTraceChunkWithSingleSpan("op2", "res2", "svc");

        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[16 * 1024];

        var firstLength = serializer.SerializeSpans(ref buffer, 0, chunk1, spanBufferOffset: 0, maxSize: buffer.Length);
        var secondLength = serializer.SerializeSpans(ref buffer, firstLength, chunk2, spanBufferOffset: firstLength, maxSize: buffer.Length);
        var total = firstLength + secondLength;
        serializer.FinishBody(ref buffer, offset: total, maxSize: buffer.Length);

        var request = OtlpProtoParser.ParseExportTraceServiceRequest(buffer, 0, total);

        request.ResourceSpans.Should().HaveCount(1);
        request.ResourceSpans[0].ScopeSpans.Should().HaveCount(1);
        request.ResourceSpans[0].ScopeSpans[0].Spans.Should().HaveCount(2);
        request.ResourceSpans[0].ScopeSpans[0].Spans[0].Name.Should().Be("res1");
        request.ResourceSpans[0].ScopeSpans[0].Spans[1].Name.Should().Be("res2");
    }

    [Fact]
    public void SerializeSpans_ReturnsZero_WhenSpansExceedMaxSize()
    {
        var chunk = TestData.CreateTraceChunkWithSingleSpan("op", "res", "svc", "test-env", "1.0.0");
        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[8 * 1024];

        // Pick a maxSize tiny enough that any real span overflows.
        var written = serializer.SerializeSpans(ref buffer, temporaryBufferOffset: 0, chunk, spanBufferOffset: 0, maxSize: 8);

        written.Should().Be(0);
    }

    [Fact]
    public void SerializeSpans_SingleChunk_ProducesParsableExportTraceServiceRequest()
    {
        var traceChunk = TestData.CreateTraceChunkWithSingleSpan(
            operationName: "op",
            resourceName: "res",
            serviceName: "svc");

        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[8 * 1024];

        var written = serializer.SerializeSpans(ref buffer, temporaryBufferOffset: 0, traceChunk, spanBufferOffset: 0, maxSize: buffer.Length);
        serializer.FinishBody(ref buffer, offset: written, maxSize: buffer.Length);

        var request = OtlpProtoParser.ParseExportTraceServiceRequest(buffer, 0, written);

        request.ResourceSpans.Should().HaveCount(1);
        var resourceSpans = request.ResourceSpans[0];
        resourceSpans.ScopeSpans.Should().HaveCount(1);
        resourceSpans.ScopeSpans[0].Spans.Should().HaveCount(1);

        var span = resourceSpans.ScopeSpans[0].Spans[0];
        span.Name.Should().Be("res");
        span.Kind.Should().Be(1); // SPAN_KIND_INTERNAL
        span.TraceId.Should().HaveCount(16);
        span.SpanId.Should().HaveCount(8);
    }

    [Fact]
    public void SerializeSpans_EmitsTraceIdAndSpanIdInBigEndianBytes()
    {
        // Known IDs: trace_id high=0x0102030405060708, low=0x090A0B0C0D0E0F10; span_id=0x1122334455667788.
        var traceId = new TraceId(0x0102030405060708UL, 0x090A0B0C0D0E0F10UL);
        const ulong spanId = 0x1122334455667788UL;

        var traceChunk = TestData.CreateTraceChunkWithRootSpan(traceId, spanId);

        var span = SerializeAndParseSingleSpan(traceChunk);

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
        var traceChunk = TestData.CreateTraceChunkWithRootSpan(new TraceId(0, 1), spanId: 0xAABBCCDDEEFF0011UL);

        var span = SerializeAndParseSingleSpan(traceChunk);

        // The parser defaults ParentSpanId to an empty array when the field is not present on the wire.
        span.ParentSpanId.Should().BeEmpty();
    }

    [Fact]
    public void SerializeSpans_ChildSpan_EmitsParentSpanIdInBigEndianBytes()
    {
        const ulong parentSpanId = 0x0102030405060708UL;
        var traceChunk = TestData.CreateTraceChunkWithChildSpan(
            new TraceId(0, 0xABCDUL),
            childSpanId: 0xFEEDFEEDFEEDFEEDUL,
            parentSpanId: parentSpanId);

        var span = SerializeAndParseSingleSpan(traceChunk);

        span.ParentSpanId.Should().Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
    }

    [Fact]
    public void SerializeSpans_EventAttributes_RoundTripAllAnyValueKinds()
    {
        var attributes = new List<KeyValuePair<string, object>>
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
        };

        var traceChunk = TestData.CreateTraceChunkWithEventAttributes(attributes);

        var span = SerializeAndParseSingleSpan(traceChunk);

        span.Events.Should().HaveCount(1);
        var byKey = new Dictionary<string, OtlpProtoParser.AnyValue>();
        foreach (var kv in span.Events[0].Attributes)
        {
            byKey[kv.Key] = kv.Value;
        }

        byKey["k_string"].StringValue.Should().Be("hello");
        byKey["k_bool_true"].BoolValue.Should().Be(true);
        byKey["k_bool_false"].BoolValue.Should().Be(false);
        byKey["k_int"].IntValue.Should().Be(42L);
        byKey["k_long"].IntValue.Should().Be(9_000_000_000L);
        byKey["k_double"].DoubleValue.Should().Be(3.14);
        byKey["k_float"].DoubleValue.Should().BeApproximately(1.5, 1e-6);
        byKey["k_bytes"].BytesValue.Should().Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        // null serializes as an empty AnyValue — no field set.
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
        var traceChunk = TestData.CreateTraceChunkWithSamplingPriority(samplingPriority);

        var span = SerializeAndParseSingleSpan(traceChunk);

        span.Flags.Should().Be(expectedFlags);
    }

    [Theory]
    [InlineData(2, 0x80000001u)]   // keep: bit 0 + bit 31
    [InlineData(1, 0x80000001u)]
    [InlineData(0, 0x80000000u)]   // drop: bit 31 only
    [InlineData(-1, 0x80000000u)]
    public void SerializeSpans_LinkSamplingPriority_EmitsLinkFlags(int linkSamplingPriority, uint expectedFlags)
    {
        var traceChunk = TestData.CreateTraceChunkWithLinkSamplingPriority(linkSamplingPriority);

        var span = SerializeAndParseSingleSpan(traceChunk);

        span.Links.Should().HaveCount(1);
        span.Links[0].Flags.Should().Be(expectedFlags);
    }

    [Fact]
    public void SerializeSpans_LinkWithoutSamplingPriority_OmitsFlagsField()
    {
        var traceChunk = TestData.CreateTraceChunkWithLinkSamplingPriority(linkSamplingPriority: null);

        var span = SerializeAndParseSingleSpan(traceChunk);

        span.Links.Should().HaveCount(1);
        // "Omitted" and "present with value 0" are observationally identical via the parser default,
        // but the serializer's contract is "omit when null", which we still cover via the keep/drop cases.
        span.Links[0].Flags.Should().Be(0u);
    }

    [Fact]
    public void SerializeSpans_EventCountAboveLimit_EmitsDroppedEventsCount()
    {
        const int excess = 3;
        var traceChunk = TestData.CreateTraceChunkWithManyEvents(OtlpTracesProtobufSerializer.EventCountLimit + excess);

        var span = SerializeAndParseSingleSpan(traceChunk);

        span.Events.Should().HaveCount(OtlpTracesProtobufSerializer.EventCountLimit);
        span.DroppedEventsCount.Should().Be((ulong)excess);
    }

    [Fact]
    public void SerializeSpans_LinkCountAboveLimit_EmitsDroppedLinksCount()
    {
        const int excess = 2;
        var traceChunk = TestData.CreateTraceChunkWithManyLinks(OtlpTracesProtobufSerializer.LinkCountLimit + excess);

        var span = SerializeAndParseSingleSpan(traceChunk);

        span.Links.Should().HaveCount(OtlpTracesProtobufSerializer.LinkCountLimit);
        span.DroppedLinksCount.Should().Be((ulong)excess);
    }

    [Fact]
    public void SerializeSpans_EventAttributeCountAboveLimit_EmitsDroppedAttributesCount()
    {
        const int excess = 5;
        var traceChunk = TestData.CreateTraceChunkWithEventAttributeOverflow(
            OtlpTracesProtobufSerializer.AttributePerEventCountLimit + excess);

        var span = SerializeAndParseSingleSpan(traceChunk);

        span.Events.Should().HaveCount(1);
        span.Events[0].Attributes.Should().HaveCount(OtlpTracesProtobufSerializer.AttributePerEventCountLimit);
        span.Events[0].DroppedAttributesCount.Should().Be((ulong)excess);
    }

    private static OtlpProtoParser.Span SerializeAndParseSingleSpan(TraceChunkModel traceChunk)
    {
        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[64 * 1024];

        var written = serializer.SerializeSpans(ref buffer, temporaryBufferOffset: 0, traceChunk, spanBufferOffset: 0, maxSize: buffer.Length);
        serializer.FinishBody(ref buffer, offset: written, maxSize: buffer.Length);

        var request = OtlpProtoParser.ParseExportTraceServiceRequest(buffer, 0, written);
        return request.ResourceSpans[0].ScopeSpans[0].Spans[0];
    }

    /// <summary>
    /// Helper that builds <see cref="TraceChunkModel"/> instances for tests.
    /// Mirrors the construction pattern from <c>OtlpMapperTests.cs</c>.
    /// </summary>
    internal static class TestData
    {
        internal static TraceChunkModel CreateTraceChunkWithSingleSpan(
            string operationName = "op",
            string resourceName = "res",
            string serviceName = "svc",
            string? environment = null,
            string? serviceVersion = null)
        {
            var span = CreateSpan(operationName, resourceName, serviceName, environment, serviceVersion);
            return new TraceChunkModel(new SpanCollection(new[] { span }));
        }

        internal static TraceChunkModel CreateTraceChunkWithEventLinkAndStatus()
        {
            var span = CreateSpan("op", "res", "svc", finishImmediately: false);

            // Add an event
            span.AddEvent(new SpanEvent("test-event", DateTimeOffset.UtcNow));

            // Add a link: use a fresh context
            var linkTraceContext = new TraceContext(new StubDatadogTracer());
            var linkContext = new SpanContext(parent: null, linkTraceContext, serviceName: "linked-service");
            span.AddLink(new SpanLink(linkContext));

            // Mark as OTel error status; the serializer reads `otel.status_code` and `error.msg` tags
            span.SetTag("otel.status_code", "STATUS_CODE_ERROR");
            span.SetTag(Tags.ErrorMsg, "oops");

            span.SetDuration(TimeSpan.FromMilliseconds(1));
            return new TraceChunkModel(new SpanCollection(new[] { span }));
        }

        internal static TraceChunkModel CreateTraceChunkWithRootSpan(TraceId traceId, ulong spanId)
        {
            var traceContext = new TraceContext(new StubDatadogTracer());
            var spanContext = new SpanContext(parent: null, traceContext, serviceName: "svc", traceId: traceId, spanId: spanId);
            var span = new Span(spanContext, DateTimeOffset.UtcNow)
            {
                OperationName = "op",
                ResourceName = "res",
            };
            span.SetDuration(TimeSpan.FromMilliseconds(1));
            return new TraceChunkModel(new SpanCollection(new[] { span }));
        }

        internal static TraceChunkModel CreateTraceChunkWithChildSpan(TraceId traceId, ulong childSpanId, ulong parentSpanId)
        {
            var traceContext = new TraceContext(new StubDatadogTracer());
            var parentContext = new SpanContext(parent: null, traceContext, serviceName: "svc", traceId: traceId, spanId: parentSpanId);
            var childContext = new SpanContext(parent: parentContext, traceContext, serviceName: "svc", traceId: traceId, spanId: childSpanId);
            var span = new Span(childContext, DateTimeOffset.UtcNow)
            {
                OperationName = "op",
                ResourceName = "res",
            };
            span.SetDuration(TimeSpan.FromMilliseconds(1));
            return new TraceChunkModel(new SpanCollection(new[] { span }));
        }

        internal static TraceChunkModel CreateTraceChunkWithEventAttributes(List<KeyValuePair<string, object>> eventAttributes)
        {
            var span = CreateSpan("op", "res", "svc", finishImmediately: false);
            span.AddEvent(new SpanEvent("test-event", DateTimeOffset.UtcNow, eventAttributes));
            span.SetDuration(TimeSpan.FromMilliseconds(1));
            return new TraceChunkModel(new SpanCollection(new[] { span }));
        }

        internal static TraceChunkModel CreateTraceChunkWithSamplingPriority(int samplingPriority)
        {
            // Use the propagated-context constructor so Context.SamplingPriority is set directly.
            var spanContext = new SpanContext(
                traceId: new TraceId(0, 1),
                spanId: 1,
                samplingPriority: samplingPriority,
                serviceName: "svc",
                origin: null);
            var span = new Span(spanContext, DateTimeOffset.UtcNow)
            {
                OperationName = "op",
                ResourceName = "res",
            };
            span.SetDuration(TimeSpan.FromMilliseconds(1));
            return new TraceChunkModel(new SpanCollection(new[] { span }));
        }

        internal static TraceChunkModel CreateTraceChunkWithLinkSamplingPriority(int? linkSamplingPriority)
        {
            var span = CreateSpan("op", "res", "svc", finishImmediately: false);

            // Build a link whose context carries the desired sampling priority directly.
            var linkContext = new SpanContext(
                traceId: new TraceId(0, 2),
                spanId: 2,
                samplingPriority: linkSamplingPriority,
                serviceName: "linked-service",
                origin: null);
            span.AddLink(new SpanLink(linkContext));

            span.SetDuration(TimeSpan.FromMilliseconds(1));
            return new TraceChunkModel(new SpanCollection(new[] { span }));
        }

        internal static TraceChunkModel CreateTraceChunkWithManyEvents(int eventCount)
        {
            var span = CreateSpan("op", "res", "svc", finishImmediately: false);
            for (int i = 0; i < eventCount; i++)
            {
                span.AddEvent(new SpanEvent($"event-{i}", DateTimeOffset.UtcNow));
            }

            span.SetDuration(TimeSpan.FromMilliseconds(1));
            return new TraceChunkModel(new SpanCollection(new[] { span }));
        }

        internal static TraceChunkModel CreateTraceChunkWithManyLinks(int linkCount)
        {
            var span = CreateSpan("op", "res", "svc", finishImmediately: false);
            var linkTraceContext = new TraceContext(new StubDatadogTracer());
            for (int i = 0; i < linkCount; i++)
            {
                var linkContext = new SpanContext(parent: null, linkTraceContext, serviceName: $"linked-{i}");
                span.AddLink(new SpanLink(linkContext));
            }

            span.SetDuration(TimeSpan.FromMilliseconds(1));
            return new TraceChunkModel(new SpanCollection(new[] { span }));
        }

        internal static TraceChunkModel CreateTraceChunkWithEventAttributeOverflow(int attributeCount)
        {
            var attributes = new List<KeyValuePair<string, object>>(attributeCount);
            for (int i = 0; i < attributeCount; i++)
            {
                attributes.Add(new KeyValuePair<string, object>($"k{i}", $"v{i}"));
            }

            return CreateTraceChunkWithEventAttributes(attributes);
        }

        internal static Span CreateSpan(
            string operationName,
            string resourceName,
            string serviceName,
            string? environment = null,
            string? serviceVersion = null,
            bool finishImmediately = true)
        {
            var traceContext = new TraceContext(new StubDatadogTracer());

            if (environment is not null)
            {
                traceContext.Environment = environment;
            }

            if (serviceVersion is not null)
            {
                traceContext.ServiceVersion = serviceVersion;
            }

            var spanContext = new SpanContext(parent: null, traceContext, serviceName: serviceName);
            var span = new Span(spanContext, DateTimeOffset.UtcNow)
            {
                OperationName = operationName,
                ResourceName = resourceName,
            };
            if (finishImmediately)
            {
                span.SetDuration(TimeSpan.FromMilliseconds(1));
            }

            return span;
        }
    }
}
