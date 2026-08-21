// <copyright file="MockOtlpAttributeValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Linq;
using OpenTelemetry.Proto.Common.V1;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// A typed OTLP attribute value. Values are kept in their native type (not stringified) so tests can
/// assert on the exact kind and value the tracer reported.
/// </summary>
public sealed class MockOtlpAttributeValue
{
    private readonly object _value;

    private MockOtlpAttributeValue(MockOtlpAttributeValueKind kind, object value)
    {
        Kind = kind;
        _value = value;
    }

    public MockOtlpAttributeValueKind Kind { get; }

    public string StringValue => (string)_value;

    public bool BoolValue => (bool)_value;

    public long IntValue => (long)_value;

    public double DoubleValue => (double)_value;

    public byte[] BytesValue => (byte[])_value;

    public IImmutableList<MockOtlpAttributeValue> ArrayValue => (IImmutableList<MockOtlpAttributeValue>)_value;

    public IImmutableList<MockOtlpAttribute> KeyValueListValue => (IImmutableList<MockOtlpAttribute>)_value;

    internal static MockOtlpAttributeValue Create(AnyValue value)
    {
        return value.ValueCase switch
        {
            AnyValue.ValueOneofCase.StringValue => new MockOtlpAttributeValue(MockOtlpAttributeValueKind.String, value.StringValue),
            AnyValue.ValueOneofCase.BoolValue => new MockOtlpAttributeValue(MockOtlpAttributeValueKind.Bool, value.BoolValue),
            AnyValue.ValueOneofCase.IntValue => new MockOtlpAttributeValue(MockOtlpAttributeValueKind.Int, value.IntValue),
            AnyValue.ValueOneofCase.DoubleValue => new MockOtlpAttributeValue(MockOtlpAttributeValueKind.Double, value.DoubleValue),
            AnyValue.ValueOneofCase.BytesValue => new MockOtlpAttributeValue(MockOtlpAttributeValueKind.Bytes, value.BytesValue.ToByteArray()),
            AnyValue.ValueOneofCase.ArrayValue => new MockOtlpAttributeValue(
                MockOtlpAttributeValueKind.Array,
                value.ArrayValue.Values.Select(Create).ToImmutableList()),
            AnyValue.ValueOneofCase.KvlistValue => new MockOtlpAttributeValue(
                MockOtlpAttributeValueKind.KeyValueList,
                value.KvlistValue.Values.Select(MockOtlpAttribute.Create).ToImmutableList()),
            _ => throw new NotSupportedException($"Unsupported OTLP attribute value case: {value.ValueCase}"),
        };
    }
}
