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

    private static CachedBytes _gitCommitSha;
    private static CachedBytes _gitRepositoryUrl;
    private static CachedBytes _aasSiteNameBytes;
    private static CachedBytes _aasSiteKindBytes;
    private static CachedBytes _aasSiteTypeBytes;
    private static CachedBytes _aasResourceGroupBytes;
    private static CachedBytes _aasSubscriptionIdBytes;
    private static CachedBytes _aasResourceIdBytes;
    private static CachedBytes _aasInstanceIdBytes;
    private static CachedBytes _aasInstanceNameBytes;
    private static CachedBytes _aasOperatingSystemBytes;
    private static CachedBytes _aasRuntimeBytes;
    private static CachedBytes _aasExtensionVersionBytes;

    public static void Clear()
    {
        _env = default;
        _version = default;
        _origin = default;
        _service = default;
        _gitCommitSha = default;
        _gitRepositoryUrl = default;
        _aasSiteNameBytes = default;
        _aasSiteKindBytes = default;
        _aasSiteTypeBytes = default;
        _aasResourceGroupBytes = default;
        _aasSubscriptionIdBytes = default;
        _aasResourceIdBytes = default;
        _aasInstanceIdBytes = default;
        _aasInstanceNameBytes = default;
        _aasOperatingSystemBytes = default;
        _aasRuntimeBytes = default;
        _aasExtensionVersionBytes = default;
    }

    public static byte[]? GetEnvironmentBytes(string? env)
    {
        return GetBytes(env, ref _env);
    }

    public static byte[]? GetVersionBytes(string? version)
    {
        return GetBytes(version, ref _version);
    }

    public static byte[]? GetGitCommitShaBytes(string? gitCommitSha)
    {
        return GetBytes(gitCommitSha, ref _gitCommitSha);
    }

    public static byte[]? GetGitRepositoryUrlBytes(string? gitRepositoryUrl)
    {
        return GetBytes(gitRepositoryUrl, ref _gitRepositoryUrl);
    }

    public static byte[]? GetOriginBytes(string? origin)
    {
        return GetBytes(origin, ref _origin);
    }

    public static byte[]? GetServiceBytes(string? service)
    {
        return GetBytes(service, ref _service);
    }

    public static byte[]? GetAzureAppServiceKeyBytes(string key, string? value)
    {
        switch (key)
        {
            case Datadog.Trace.Tags.AzureAppServicesSiteName:
                return GetBytes(value, ref _aasSiteNameBytes);
            case Datadog.Trace.Tags.AzureAppServicesSiteType:
                return GetBytes(value, ref _aasSiteTypeBytes);
            case Datadog.Trace.Tags.AzureAppServicesSiteKind:
                return GetBytes(value, ref _aasSiteKindBytes);
            case Datadog.Trace.Tags.AzureAppServicesResourceGroup:
                return GetBytes(value, ref _aasResourceGroupBytes);
            case Datadog.Trace.Tags.AzureAppServicesSubscriptionId:
                return GetBytes(value, ref _aasSubscriptionIdBytes);
            case Datadog.Trace.Tags.AzureAppServicesResourceId:
                return GetBytes(value, ref _aasResourceIdBytes);
            case Datadog.Trace.Tags.AzureAppServicesInstanceId:
                return GetBytes(value, ref _aasInstanceIdBytes);
            case Datadog.Trace.Tags.AzureAppServicesInstanceName:
                return GetBytes(value, ref _aasInstanceNameBytes);
            case Datadog.Trace.Tags.AzureAppServicesOperatingSystem:
                return GetBytes(value, ref _aasOperatingSystemBytes);
            case Datadog.Trace.Tags.AzureAppServicesRuntime:
                return GetBytes(value, ref _aasRuntimeBytes);
            case Datadog.Trace.Tags.AzureAppServicesExtensionVersion:
                return GetBytes(value, ref _aasExtensionVersionBytes);
            default:
                throw new InvalidOperationException("The given key isn't yet handled in the cache");
        }
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
