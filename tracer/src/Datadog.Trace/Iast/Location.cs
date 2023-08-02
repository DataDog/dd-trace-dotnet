// <copyright file="Location.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;

internal readonly struct Location
{
    public Location(string fullPath)
    {
        Path = fullPath;
    }

    public Location(string? stackFile, string? methodName, int? line, ulong? spanId, string? methodTypeName)
    {
        if (!string.IsNullOrEmpty(stackFile))
        {
            this.Path = System.IO.Path.GetFileName(stackFile);
            this.Line = line;
        }
        else
        {
            this.Method = methodName;
            this.Path = methodTypeName;
        }

        this.SpanId = spanId == 0 ? null : spanId;
    }

    public ulong? SpanId { get; }

    public string? Path { get; }

    public string? Method { get; }

    public int? Line { get; }

    public override int GetHashCode()
    {
        // We do not calculate the hash including the spanId
        return IastUtils.GetHashCode(Path, Line, Method);
    }
}
