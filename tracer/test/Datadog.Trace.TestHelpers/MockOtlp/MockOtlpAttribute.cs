// <copyright file="MockOtlpAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using OpenTelemetry.Proto.Common.V1;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// A single OTLP key/value attribute.
/// </summary>
public sealed class MockOtlpAttribute
{
    private MockOtlpAttribute(string key, MockOtlpAttributeValue value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }

    public MockOtlpAttributeValue Value { get; }

    internal static MockOtlpAttribute Create(KeyValue keyValue)
        => new(keyValue.Key, MockOtlpAttributeValue.Create(keyValue.Value));
}
