// <copyright file="Enums.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Expressions;

internal enum ProbeType
{
    Log = 0,
    Snapshot = 1,
    Metric = 2,
    SpanDecoration = 3
}

internal enum ProbeLocation
{
    Method,
    Line,
}

internal enum ProbeProcessorResult
{
    Continue,
    ContinueWithoutCapturing,
    Stop
}

internal enum MethodState
{
    EntryStart,
    EntryEnd,
    ExitStart,
    ExitEnd,
    EntryAsync,
    ExitStartAsync,
    ExitEndAsync,
    BeginLine,
    EndLine,
    BeginLineAsync,
    EndLineAsync,
    LogArg,
    LogLocal,
}

internal enum CaptureBehaviour
{
    /// <summary>
    /// Capture values
    /// </summary>
    Capture,

    /// <summary>
    /// Delay the capture process until expression will be evaluated
    /// </summary>
    Delay,

    /// <summary>
    /// Do not capture in this state. e.g. we are in entry state and we need to evaluate condition at exit
    /// </summary>
    NoCapture,

    /// <summary>
    /// Evaluate the expression\s and then continue
    /// </summary>
    Evaluate,

    /// <summary>
    /// Stop capture completely if a condition has evaluated to false or unpredicted error occurred
    /// </summary>
    Stop
}

internal static class Enums
{
    internal static bool IsInEntry(this MethodState state)
    {
        return state is MethodState.BeginLine
                   or MethodState.BeginLineAsync
                   or MethodState.EntryStart
                   or MethodState.EntryAsync
                   or MethodState.EntryEnd;
    }

    internal static bool IsInEntryEnd(this MethodState state)
    {
        return state is MethodState.EndLine
                   or MethodState.EndLineAsync
                   or MethodState.EntryEnd
                   or MethodState.EntryAsync;
    }

    internal static bool IsInExitEnd(this MethodState state)
    {
        return state is MethodState.ExitEndAsync or MethodState.ExitEnd;
    }

    internal static bool IsInStartMarkerOrBeginLine(this MethodState state)
    {
        return state is MethodState.BeginLine
                   or MethodState.BeginLineAsync
                   or MethodState.EntryStart
                   or MethodState.EntryAsync
                   or MethodState.ExitStart
                   or MethodState.ExitStartAsync;
    }

    internal static bool IsInExit(this MethodState state)
    {
        return state is MethodState.EndLine
                   or MethodState.EndLineAsync
                   or MethodState.ExitStart
                   or MethodState.ExitStartAsync
                   or MethodState.ExitEnd
                   or MethodState.ExitEndAsync;
    }
}
