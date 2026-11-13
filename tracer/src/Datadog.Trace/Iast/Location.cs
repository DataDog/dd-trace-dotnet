// <copyright file="Location.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast;

internal readonly struct Location
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Location>();

    // Both Windows ('\') and Unix ('/') separators, because PDBs produced on one OS may be read on another.
    private static readonly char[] PathSeparators = ['/', '\\'];

    internal readonly StackTrace? _stack = null;

    // What deduplicates the vulnerability, which may differ from the reported Class/Method
    private readonly string? _identityClass;
    private readonly string? _identityMethod;

    public Location(string method)
    {
        var index = method.LastIndexOf("::", StringComparison.Ordinal);
        if (index >= 0)
        {
            Class = method.Substring(0, length: index);
            var bracketIndex = method.IndexOf("(", startIndex: index + 2, StringComparison.Ordinal);
            Method = bracketIndex > 0
                         ? method.Substring(index + 2, length: bracketIndex - index - 2)
                         : method.Substring(index + 2);
        }
        else
        {
            Method = method;
        }

        _identityClass = Class;
        _identityMethod = Method;
    }

    public Location(StackFrame? stackFrame, StackTrace? stack, string? stackId, ulong? spanId, StackFrame? identityFrame = null)
    {
        var method = stackFrame?.GetMethod();
        var sourceMethod = GetSourceMethod(method);
        Class = sourceMethod?.DeclaringType?.FullName;
        Method = sourceMethod?.Name;

        // the identity keeps the frame's own method, state machine included, because it is what the
        // vulnerability hash, and so the backend, identifies the vulnerability by
        var identityMethod = identityFrame is null || identityFrame == stackFrame ? method : identityFrame.GetMethod();
        _identityClass = identityMethod?.DeclaringType?.FullName;
        _identityMethod = identityMethod?.Name;

        var line = stackFrame?.GetFileLineNumber();
        Line = line > 0 ? line : null;
        Path = GetFileName(stackFrame?.GetFileName());

        SpanId = spanId == 0 ? null : spanId;

        _stack = stack;
        StackId = stackId;
    }

    internal Location(string? typeName, string? methodName, int? line, ulong? spanId) // For testing purposes only
    {
        this.Class = typeName;
        this.Method = methodName;
        Line = line > 0 ? line : null;
        _identityClass = typeName;
        _identityMethod = methodName;

        this.SpanId = spanId == 0 ? null : spanId;
    }

    public ulong? SpanId { get; }

    public string? Path { get; }

    public string? Class { get; }

    public string? Method { get; }

    public int? Line { get; }

    public string? StackId { get; }

    public override int GetHashCode()
    {
        // We do not calculate the hash including the spanId nor the line
        return IastUtils.GetHashCode(_identityClass, _identityMethod);
    }

    // The MoveNext of an async method or an iterator is reported as the method it was generated from
    private static MethodBase? GetSourceMethod(MethodBase? method)
    {
        try
        {
            if (method is not null && StateMachineHelper.TryGetSourceMethod(method, out var sourceMethod))
            {
                return sourceMethod;
            }
        }
        catch (Exception ex)
        {
            // reading the attributes of the customer's methods can fail when an assembly they reference is missing,
            // which happens again for every vulnerability raised there, so not worth more than a debug log
            Log.Debug(ex, "Could not resolve the source method of the state machine {Type}", method?.DeclaringType?.FullName);
        }

        return method;
    }

    // Extracts the file name from a path, handling both Windows ('\') and Unix ('/') separators.
    // We can't rely on System.IO.Path.GetFileName here because it only recognizes the current OS
    // separator, so a Windows path read on Unix (from a Windows-built PDB) would leak the full path.
    private static string? GetFileName(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        var separatorIndex = filePath!.LastIndexOfAny(PathSeparators);
        return separatorIndex >= 0 ? filePath.Substring(separatorIndex + 1) : filePath;
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
                span.Context.TraceContext?.AppSecRequestContext.AddVulnerabilityStackTrace(stack, Security.Instance.Settings.MaxStackTraces);
            }
        }
    }
}
