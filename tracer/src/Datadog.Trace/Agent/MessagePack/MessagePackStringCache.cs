// <copyright file="MessagePackStringCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent.MessagePack;

/// <summary>
/// Caches string values encoded as MessagePack bytes. These are not plain UTF-8 strings,
/// they include the MessagePack header for each string as well.
/// Use these byte arrays with MessagePackBinary.WriteRaw().
/// </summary>
internal static class MessagePackStringCache
{
    [ThreadStatic]
    private static CachedBytes _env;

    [ThreadStatic]
    private static CachedBytes _version;

    [ThreadStatic]
    private static CachedBytes _origin;

    public static byte[] GetEnvironmentBytes(string? env)
    {
        return GetBytes(env, ref _env);
    }

    public static byte[] GetVersionBytes(string? version)
    {
        return GetBytes(version, ref _version);
    }

    public static byte[] GetOriginBytes(string? origin)
    {
        return GetBytes(origin, ref _origin);
    }

    private static byte[] GetBytes(string? value, ref CachedBytes cachedBytes)
    {
        if (cachedBytes.String == value)
        {
            // return the cached bytes
            return cachedBytes.Bytes;
        }

        // encode the string into MessagePack and cache the bytes before returning them
        var converted = MessagePackSerializer.Serialize(value);
        cachedBytes = new CachedBytes(value, converted);
        return converted;
    }

    private readonly struct CachedBytes
    {
        public readonly string? String;

        public readonly byte[] Bytes;

        public CachedBytes(string? @string, byte[] bytes)
        {
            String = @string;
            Bytes = bytes;
        }
    }
}
