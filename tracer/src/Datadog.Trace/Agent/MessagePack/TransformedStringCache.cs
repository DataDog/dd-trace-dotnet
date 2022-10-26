// <copyright file="TransformedStringCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Agent.MessagePack;

/// <summary>
/// Caches string values encoded as MessagePack bytes. These are not plain UTF-8 strings,
/// they include the MessagePack header for each string as well.
/// Use these byte arrays with MessagePackBinary.WriteRaw().
/// </summary>
internal class TransformedStringCache<T>
{
    [ThreadStatic]
    private static CachedValue _cachedValue;

    private readonly Func<string?, T?> _transform;

    public TransformedStringCache(Func<string?, T?> transform)
    {
        _transform = transform;
    }

    public T? GetTransformedValue(string? value)
    {
        var cachedValue = _cachedValue;

        if (cachedValue.OriginValue == value)
        {
            // return the previously transformed value
            return cachedValue.TransformedValue;
        }

        // transform string and cache the value before returning it
        var converted = _transform(value);
        _cachedValue = new CachedValue(value, converted);
        return converted;
    }

    private readonly struct CachedValue
    {
        public readonly string? OriginValue;

        public readonly T? TransformedValue;

        public CachedValue(string? originValue, T? transformedValue)
        {
            OriginValue = originValue;
            TransformedValue = transformedValue;
        }
    }
}
