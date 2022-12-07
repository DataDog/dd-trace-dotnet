// <copyright file="Location.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;

internal readonly struct Location
{
    public Location(string? path, int? line)
    {
        this.Path = path;
        this.Line = line;
    }

    public string? Path { get; }

    public int? Line { get; }

    public override int GetHashCode()
    {
        return IastUtils.GetHashCode(Path, Line);
    }
}
