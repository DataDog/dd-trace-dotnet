// <copyright file="CachedMessagePackBytes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent.MessagePack;

/// <summary>
/// Caches string values encoded as MessagePack bytes. These are not plain UTF-8 strings,
/// they include the MessagePack header for each string as well.
/// Use these byte arrays with MessagePackBinary.WriteRaw().
/// </summary>
internal readonly struct CachedMessagePackBytes
{
    public readonly byte[]? Environment;

    public readonly byte[]? ServiceVersion;

    public readonly byte[]? Origin;

    public CachedMessagePackBytes(
        string? environment,
        string? serviceVersion,
        string? origin)
    {
        Environment = GetBytes(environment);
        ServiceVersion = GetBytes(serviceVersion);
        Origin = GetBytes(origin);
    }

    private static byte[]? GetBytes(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : MessagePackSerializer.Serialize(value);
    }
}
