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

    public CachedStringBytes(Encoding encoding, string? environment, string? serviceVersion)
    {
        Environment = !string.IsNullOrEmpty(environment) ? encoding.GetBytes(environment) : null;
        ServiceVersion = !string.IsNullOrEmpty(serviceVersion) ? encoding.GetBytes(serviceVersion) : null;
    }
}
