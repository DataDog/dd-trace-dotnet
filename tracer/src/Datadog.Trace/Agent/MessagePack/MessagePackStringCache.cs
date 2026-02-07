// <copyright file="MessagePackStringCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent.MessagePack;

/// <summary>
/// A cache of string values encoded as MessagePack bytes. These strings are usually
/// constants for the lifetime of a service, but that is not guaranteed, so we cache
/// only a single value as long as it doesn't change.
///
/// These are not UTF-8 strings. They also include the MessagePack header for each string.
/// Use MessagePackBinary.WriteRaw() to write these byte arrays, not MessagePackBinary.WriteStringBytes().
/// </summary>
internal static class MessagePackStringCache
{
    [ThreadStatic]
    private static CachedBytes _env;

    [ThreadStatic]
    private static CachedBytes _version;

    [ThreadStatic]
    private static CachedBytes _origin;

    [ThreadStatic]
    private static CachedBytes _service;

    // Today, process tags may change only when the service name isn't the default one anymore
    [ThreadStatic]
    private static CachedBytes _processTags;

    [ThreadStatic]
    private static CachedBytes _gitCommitSha;

    [ThreadStatic]
    private static CachedBytes _gitRepositoryUrl;

    public static void Clear()
    {
        _env = default;
        _version = default;
        _origin = default;
        _service = default;
        _processTags = default;
        _gitCommitSha = default;
        _gitRepositoryUrl = default;
    }

    public static byte[]? GetEnvironmentBytes(string? env)
    {
        return GetBytes(env, ref _env);
    }

    public static byte[]? GetVersionBytes(string? version)
    {
        return GetBytes(version, ref _version);
    }

    public static byte[]? GetOriginBytes(string? origin)
    {
        return GetBytes(origin, ref _origin);
    }

    public static byte[]? GetServiceBytes(string? service)
    {
        return GetBytes(service, ref _service);
    }

    public static byte[]? GetProcessTagsBytes(string? processTags)
    {
        return GetBytes(processTags, ref _processTags);
    }

    public static byte[]? GetGitCommitShaBytes(string? gitCommitSha)
    {
        return GetBytes(gitCommitSha, ref _gitCommitSha);
    }

    public static byte[]? GetGitRepositoryUrlBytes(string? gitRepositoryUrl)
    {
        return GetBytes(gitRepositoryUrl, ref _gitRepositoryUrl);
    }

    private static byte[]? GetBytes(string? value, ref CachedBytes cachedBytes)
    {
        var localCachedBytes = cachedBytes;

        if (localCachedBytes.String == value)
        {
            // return the cached bytes
            return localCachedBytes.Bytes;
        }

        // encode the string into MessagePack and cache the bytes before returning them
        var bytes = string.IsNullOrWhiteSpace(value) ? null : MessagePackSerializer.Serialize(value);
        cachedBytes = new CachedBytes(value, bytes);
        return bytes;
    }

    private readonly struct CachedBytes
    {
        public readonly string? String;

        public readonly byte[]? Bytes;

        public CachedBytes(string? @string, byte[]? bytes)
        {
            String = @string;
            Bytes = bytes;
        }
    }
}
