// <copyright file="KeyValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Common;

/// <summary>
/// Placeholder for opentelemetry.proto.common.v1.KeyValue
/// This should be defined in a separate file or referenced from existing KeyValue struct.
/// </summary>
internal readonly struct KeyValue
{
    public readonly string Key;
    public readonly object? Value;

    public KeyValue(string key, object? value)
    {
        Key = key;
        Value = value;
    }
}
