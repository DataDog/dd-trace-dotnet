// <copyright file="MockOtlpAttributeValueKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// The kind of value carried by a <see cref="MockOtlpAttributeValue"/>, mirroring OTLP's <c>AnyValue</c> oneof.
/// </summary>
public enum MockOtlpAttributeValueKind
{
    String,
    Bool,
    Int,
    Double,
    Bytes,
    Array,
    KeyValueList,
}
