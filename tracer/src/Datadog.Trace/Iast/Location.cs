// <copyright file="Location.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;

namespace Datadog.Trace.Iast;

internal readonly struct Location
{
    public Location(string method)
    {
        var index = method.LastIndexOf("::", StringComparison.Ordinal);
        if (index >= 0)
        {
            Path = method.Substring(0, length: index);
            var bracketIndex = method.IndexOf("(", startIndex: index + 2, StringComparison.Ordinal);
            Method = bracketIndex > 0
                         ? method.Substring(index + 2, length: bracketIndex - index - 2)
                         : method.Substring(index + 2);
        }
        else
        {
            Method = method;
        }
    }

    public Location(StackFrame? stackFrame, ulong? spanId)
    {
        var method = stackFrame?.GetMethod();
        Path = method?.DeclaringType?.FullName;
        Method = method?.Name;
        var line = stackFrame?.GetFileLineNumber();
        Line = line > 0 ? line : null;

        SpanId = spanId == 0 ? null : spanId;
    }

    public Location(string? typeName, string? methodName, int? line, ulong? spanId)
    {
        this.Path = typeName;
        this.Method = methodName;
        Line = line > 0 ? line : null;

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
