// <copyright file="ProbeEnums.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Expressions;

internal enum ProbeType
{
    Log = 0,
    Snapshot = 1,
    Metric = 2
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
    LogException,
}

internal enum CaptureBehaviour
{
    Capture,
    Delayed,
    NoCapture
}
