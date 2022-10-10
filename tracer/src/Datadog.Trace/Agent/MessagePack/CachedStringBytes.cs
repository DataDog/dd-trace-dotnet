// <copyright file="CachedStringBytes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Text;

namespace Datadog.Trace.Agent.MessagePack;

internal readonly struct CachedStringBytes
{
    public readonly byte[]? Environment;

    public readonly byte[]? ServiceVersion;

    public readonly byte[]? Origin;

    public CachedStringBytes(Encoding encoding, string? environment, string? serviceVersion, string? origin)
    {
        Environment = GetBytes(encoding, environment);
        ServiceVersion = GetBytes(encoding, serviceVersion);
        Origin = GetBytes(encoding, origin);
    }

    private static byte[]? GetBytes(Encoding encoding, string? value)
    {
        return string.IsNullOrEmpty(value) ? null : encoding.GetBytes(value);
    }
}
