// <copyright file="TagItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tagging;

internal readonly ref struct TagItem<T>
{
    public readonly string Key;
    public readonly T Value;

    public readonly ReadOnlySpan<byte> SerializedKey;

    public TagItem(string key, T value, ReadOnlySpan<byte> serializedKey)
    {
        Key = key;
        Value = value;
        SerializedKey = serializedKey;
    }
}
