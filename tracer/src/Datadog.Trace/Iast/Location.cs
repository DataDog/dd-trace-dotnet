// <copyright file="Location.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rasp;

namespace Datadog.Trace.Iast;

internal readonly struct Location
{
    internal readonly StackTrace? _stack = null;

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

    public Location(StackFrame? stackFrame, StackTrace? stack, string? stackId, ulong? spanId)
    {
        var method = stackFrame?.GetMethod();
        Path = method?.DeclaringType?.FullName;
        Method = method?.Name;
        var line = stackFrame?.GetFileLineNumber();
        Line = line > 0 ? line : null;

        SpanId = spanId == 0 ? null : spanId;

        _stack = stack;
        StackId = stackId;
    }

    internal Location(string? typeName, string? methodName, int? line, ulong? spanId) // For testing purposes only
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

    public string? StackId { get; }

    public override int GetHashCode()
    {
        // We do not calculate the hash including the spanId nor the line
        return IastUtils.GetHashCode(Path, Method);
    }

    internal void ReportStack(Span? span)
    {
        if (span is not null && StackId is not null && _stack is not null && _stack.FrameCount > 0)
        {
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types. Some TFMs (pre net 6) don't have null annotations
            var stack = StackReporter.GetStack(Security.Instance.Settings.MaxStackTraceDepth, Security.Instance.Settings.MaxStackTraceDepthTopPercent, StackId, _stack.GetFrames());
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
            if (stack is not null)
            {
                span.Context.TraceContext?.AddVulnerabilityStackTraceElement(stack, Security.Instance.Settings.MaxStackTraces);
            }
        }
    }
}
