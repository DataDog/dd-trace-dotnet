// <copyright file="MockOtlpEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;

// Datadog.Trace.Span (the real, internal tracer span type) shadows the unqualified "Span" name from
// OpenTelemetry.Proto.Trace.V1 for any type in this file's namespace tree, since ancestor-namespace
// members take priority over using-directive imports. Alias it, matching the existing convention in
// OtlpTracesProtobufSerializerTests.cs.
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// A span event, as reported over OTLP.
/// </summary>
public sealed class MockOtlpEvent
{
    private MockOtlpEvent(string name, ulong timeUnixNano, IImmutableList<MockOtlpAttribute> attributes, uint droppedAttributesCount)
    {
        Name = name;
        TimeUnixNano = timeUnixNano;
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
    }

    public string Name { get; }

    public ulong TimeUnixNano { get; }

    public IImmutableList<MockOtlpAttribute> Attributes { get; }

    public uint DroppedAttributesCount { get; }

    internal static MockOtlpEvent Create(OtlpSpan.Types.Event e)
        => new(e.Name, e.TimeUnixNano, e.Attributes.Select(MockOtlpAttribute.Create).ToImmutableList(), e.DroppedAttributesCount);
}
