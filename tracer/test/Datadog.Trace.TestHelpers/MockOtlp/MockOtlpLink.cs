// <copyright file="MockOtlpLink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using Datadog.Trace.Util;

// See the comment in MockOtlpEvent.cs about why this alias is necessary.
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// A span link, as reported over OTLP. <see cref="TraceId"/>/<see cref="SpanId"/> are lowercase hex strings,
/// matching the convention used for the owning span's own IDs.
/// </summary>
public sealed class MockOtlpLink
{
    private MockOtlpLink(string traceId, string spanId, string traceState, IImmutableList<MockOtlpAttribute> attributes, uint droppedAttributesCount, uint flags)
    {
        TraceId = traceId;
        SpanId = spanId;
        TraceState = traceState;
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
        Flags = flags;
    }

    public string TraceId { get; }

    public string SpanId { get; }

    public string TraceState { get; }

    public IImmutableList<MockOtlpAttribute> Attributes { get; }

    public uint DroppedAttributesCount { get; }

    public uint Flags { get; }

    internal static MockOtlpLink Create(OtlpSpan.Types.Link link)
        => new(
            HexString.ToHexString(link.TraceId.ToByteArray()),
            HexString.ToHexString(link.SpanId.ToByteArray()),
            link.TraceState,
            link.Attributes.Select(MockOtlpAttribute.Create).ToImmutableList(),
            link.DroppedAttributesCount,
            link.Flags);
}
