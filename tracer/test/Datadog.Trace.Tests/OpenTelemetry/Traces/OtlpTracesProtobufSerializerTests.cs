// <copyright file="OtlpTracesProtobufSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
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

        internal static Span CreateSpan(
            string operationName,
            string resourceName,
            string serviceName,
            string? environment = null,
            string? serviceVersion = null)
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
            span.SetDuration(TimeSpan.FromMilliseconds(1));
            return span;
        }
    }
}

#endif
