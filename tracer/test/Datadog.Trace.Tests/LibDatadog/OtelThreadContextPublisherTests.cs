// <copyright file="OtelThreadContextPublisherTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTrace;

#nullable enable

using System;
using Datadog.Trace.LibDatadog.OtelThreadContext;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OtelThreadContext;

public class OtelThreadContextPublisherTests
{
    private const ulong ExpectedSpanId = 0x1020304050607080;
    private const ulong ExpectedLocalRootSpanId = 0xFFEEDDCCBBAA9988;

    private static readonly TraceId ExpectedTraceId = new(0x0011223344556677, 0x8899AABBCCDDEEFF);
    private static readonly byte[] ExpectedTraceIdBytes =
    [
        0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77,
        0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF
    ];

    [Fact]
    public void SetSerializesIdsInBigEndian()
    {
        var nativeMethods = new RecordingNativeMethods();
        var publisher = new OtelThreadContextPublisher(nativeMethods);
        var span = CreateChildSpan();

        publisher.Set(span);

        nativeMethods.UpdateCalls.Should().Be(1);
        nativeMethods.TraceId.Should().Equal(ExpectedTraceIdBytes);
        nativeMethods.SpanId.Should().Equal(0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80);
        nativeMethods.LocalRootSpanId.Should().Equal(0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88);
    }

    [Fact]
    public void ResetClearsContextInPlaceWithZeroIds()
    {
        var nativeMethods = new RecordingNativeMethods();
        var publisher = new OtelThreadContextPublisher(nativeMethods);

        publisher.Reset();

        nativeMethods.UpdateCalls.Should().Be(1);
        nativeMethods.TraceId.Should().Equal(new byte[TraceId.Size]);
        nativeMethods.SpanId.Should().Equal(new byte[sizeof(ulong)]);
        nativeMethods.LocalRootSpanId.Should().Equal(new byte[sizeof(ulong)]);
    }

    [Fact]
    public void NativeFailureDisablesPublisher()
    {
        var nativeMethods = new RecordingNativeMethods { UpdateException = new EntryPointNotFoundException() };
        var publisher = new OtelThreadContextPublisher(nativeMethods);
        var span = CreateChildSpan();

        publisher.Set(span);
        publisher.Set(span);
        publisher.Reset();

        publisher.IsEnabled.Should().BeFalse();
        nativeMethods.UpdateCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void FactoryRequiresEnablementAndSupportedDeployment(
        bool enabled,
        bool platformIsSupported,
        bool deploymentIsSupported,
        bool expectedEnabled)
    {
        var nativeMethods = new RecordingNativeMethods();

        var publisher = OtelThreadContextPublisher.Create(enabled, platformIsSupported, deploymentIsSupported, nativeMethods);

        publisher.IsEnabled.Should().Be(expectedEnabled);
        publisher.Set(CreateChildSpan());
        publisher.Reset();
        nativeMethods.UpdateCalls.Should().Be(expectedEnabled ? 2 : 0);
    }

    private static Span CreateChildSpan()
    {
        var traceContext = new TraceContext(new StubDatadogTracer());
        var rootContext = new SpanContext(
            parent: null,
            traceContext: traceContext,
            serviceName: "service",
            traceId: ExpectedTraceId,
            spanId: ExpectedLocalRootSpanId);
        var rootSpan = new Span(rootContext, DateTimeOffset.UtcNow);
        traceContext.AddSpan(rootSpan);

        var childContext = new SpanContext(rootContext, traceContext, "service", spanId: ExpectedSpanId);
        var childSpan = new Span(childContext, DateTimeOffset.UtcNow);
        traceContext.AddSpan(childSpan);
        return childSpan;
    }

    private sealed class RecordingNativeMethods : IOtelThreadContextNativeMethods
    {
        public int UpdateCalls { get; private set; }

        public byte[]? TraceId { get; private set; }

        public byte[]? SpanId { get; private set; }

        public byte[]? LocalRootSpanId { get; private set; }

        public Exception? UpdateException { get; init; }

#if NETFRAMEWORK || NETCOREAPP2_1 || NETCOREAPP3_0
        public void Update(DatadogTrace::System.ReadOnlySpan<byte> traceId, DatadogTrace::System.ReadOnlySpan<byte> spanId, DatadogTrace::System.ReadOnlySpan<byte> localRootSpanId)
#else
        public void Update(ReadOnlySpan<byte> traceId, ReadOnlySpan<byte> spanId, ReadOnlySpan<byte> localRootSpanId)
#endif
        {
            UpdateCalls++;
            if (UpdateException is not null)
            {
                throw UpdateException;
            }

            TraceId = traceId.ToArray();
            SpanId = spanId.ToArray();
            LocalRootSpanId = localRootSpanId.ToArray();
        }
    }
}
